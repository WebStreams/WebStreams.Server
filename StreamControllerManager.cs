// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   The stream controller manager.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStream.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net.WebSockets;
    using System.Reactive;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Reactive.Threading.Tasks;
    using System.Reflection;
    using System.Threading.Tasks;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// The stream controller manager.
    /// </summary>
    internal class StreamControllerManager
    {
        /// <summary>
        /// The stream controllers.
        /// </summary>
        private readonly ConcurrentDictionary<Type, object> streamControllers = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Describes a stream controller method invoker.
        /// </summary>
        /// <param name="controller">
        /// The controller.
        /// </param>
        /// <param name="parameters">
        /// The parameters dictionary.
        /// </param>
        /// <param name="getObservable">
        /// The delegate used to retrieve the <see cref="IObservable{T}"/> for a provided parameter.
        /// </param>
        /// <returns>
        /// The resulting stream.
        /// </returns>
        public delegate IObservable<string> Invoker(object controller, IDictionary<string, string> parameters, Func<string, IObservable<string>> getObservable);

        /// <summary>
        /// Returns a handler for incoming Web stream requests.
        /// </summary>
        /// <param name="handler">
        /// The handler.
        /// </param>
        /// <param name="controller">
        /// The controller.
        /// </param>
        /// <param name="args">
        /// The request parameters.
        /// </param>
        /// <returns>
        /// A handler for incoming Web stream requests.
        /// </returns>
        public static Func<IDictionary<string, object>, Task> CreateStreamHandler(
            Invoker handler,
            object controller,
            IDictionary<string, string> args)
        {
            return async environment =>
            {
                using (var socket = new WebSocket(environment))
                {
                    var incoming = new Dictionary<string, ISubject<string>>();
                    Func<string, IObservable<string>> getIncoming = name =>
                    {
                        ISubject<string> result;
                        if (!incoming.TryGetValue(name, out result))
                        {
                            result = new QueueSubject<string>();
                            incoming.Add(name, result);
                        }

                        return result;
                    };

                    // Hook up the incoming and outgoing message pumps.
                    var outgoing = GetObservableFromHandler(() => handler(controller, args, getIncoming));
                    var outgoingMessagePump = SubscribeViaSocket(outgoing, socket);
                    var incomingMessagePump = SubscribeManyToSocket(socket, incoming);

                    // Close the socket when both pumps finish.
                    await Task.WhenAll(outgoingMessagePump, incomingMessagePump);
                }
            };
        }

        /// <summary>
        /// Returns the <see cref="Invoker"/> for the provided <paramref name="method"/>.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <param name="serializerSettings">
        /// The serializer settings.
        /// </param>
        /// <returns>
        /// The <see cref="Invoker"/> for the provided <paramref name="method"/>.
        /// </returns>
        public Invoker GetInvoker(MethodInfo method, JsonSerializerSettings serializerSettings)
        {
            // Define the parameters of the resulting invoker.
            var controllerParameter = Expression.Parameter(typeof(object), "controller");
            var parametersParameter = Expression.Parameter(typeof(IDictionary<string, string>), "parameters");
            var getObservableParameter = Expression.Parameter(typeof(Func<string, IObservable<string>>), "getObservable");

            // Reflect the methods being which are used below.
            var tryGetValue = typeof(IDictionary<string, string>).GetMethod("TryGetValue");
            var deserialize = typeof(JsonConvert).GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type), typeof(JsonSerializerSettings) });
            var invokeFunc = typeof(Func<string, IObservable<string>>).GetMethod("Invoke");

            // Construct expressions to retrieve each of the controller method's parameters.
            var parameterDictionaryVar = Expression.Variable(typeof(string), "paramVal");
            var allVariables = new List<ParameterExpression> { parameterDictionaryVar };
            var parameters = new List<ParameterExpression>();
            var parameterAssignments = new List<Expression>();
            foreach (var parameter in method.GetParameters())
            {
                var name = parameter.Name.ToLowerInvariant();

                // Resulting parameter value.
                var paramType = parameter.ParameterType;
                var paramVar = Expression.Variable(paramType, parameter.Name);
                parameters.Add(paramVar);
                Expression parameterAssignment;

                // If the parameter is an observable, get the incoming stream using the "getObservable" parameter.
                var serializerSettingsConst = Expression.Constant(serializerSettings);
                if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(IObservable<>))
                {
                    // This is an incoming observable, get the proxy observable to pass in.
                    var incomingObservable = Expression.Call(getObservableParameter, invokeFunc, new Expression[] { Expression.Constant(name) });

                    // Select the proxy observable into the correct shape.
                    var paramTypeArg = paramType.GenericTypeArguments[0];
                    var selectIncomingObservable = typeof(Observable).GetGenericMethod(
                        "Select",
                        new[] { typeof(IObservable<string>), typeof(Func<,>).MakeGenericType(typeof(string), paramTypeArg) },
                        new[] { typeof(string), paramTypeArg });
                    var next = Expression.Parameter(typeof(string), "next");
                    var observableType = paramTypeArg;
                    var selector =
                        Expression.Lambda(
                            Expression.Convert(
                                Expression.Call(
                                    null,
                                    deserialize,
                                    new Expression[] { next, Expression.Constant(observableType), serializerSettingsConst }),
                                observableType),
                            new[] { next });

                    // Pass the converted observable in for the current parameter.
                    parameterAssignment = Expression.Assign(
                        paramVar,
                        Expression.Call(null, selectIncomingObservable, new Expression[] { incomingObservable, selector }));
                }
                else
                {
                    // Try to get the parameter from the parameters dictionary and convert it if neccessary.
                    Expression convertParam;
                    var tryGetParam = Expression.Call(parametersParameter, tryGetValue, new Expression[] { Expression.Constant(name), parameterDictionaryVar });

                    if (paramType == typeof(string))
                    {
                        // Strings need no conversion, just pluck the value from the parameter list.
                        convertParam = parameterDictionaryVar;
                    }
                    else
                    {
                        // Determine whether or not the standard
                        if (paramType.ShouldUseStaticTryParseMthod())
                        {
                            // Parse the value using the "TryParse" method of the parameter type.
                            var tryParseMethod = paramType.GetMethod("TryParse", new[] { typeof(string), paramType.MakeByRefType() });
                            var tryParseExp = Expression.Call(tryParseMethod, parameterDictionaryVar, paramVar);

                            // Use the default value if parsing failed.
                            convertParam = Expression.Block(tryParseExp, paramVar);
                        }
                        else
                        {
                            // Determine whether or not the value is a JSON primitive.
                            var contract = JsonSerializer.Create(serializerSettings).ContractResolver.ResolveContract(paramType);
                            var isPrimitive = contract.GetType() == typeof(JsonPrimitiveContract);

                            // Use the provided serializer to deserialize the parameter value.
                            var paramTypeConst = Expression.Constant(paramType);
                            if (isPrimitive)
                            {
                                // String-based primitives such as DateTime need to be wrapped in quotes before deserialization.
                                var quoteConst = Expression.Constant("\"");
                                var stringConcatMethod = typeof(string).GetMethod("Concat", new[] { typeof(object), typeof(object), typeof(object) });
                                var quotedParameterValue = Expression.Call(
                                    null,
                                    stringConcatMethod,
                                    new Expression[] { quoteConst, parameterDictionaryVar, quoteConst });

                                // Deserialize the quoted value.
                                var deserialized = Expression.Call(
                                    null,
                                    deserialize,
                                    new Expression[] { quotedParameterValue, paramTypeConst, serializerSettingsConst });
                                convertParam = Expression.Convert(deserialized, paramType);
                            }
                            else
                            {
                                // Serialize raw value for non-primitive types, bools, and numeric types.
                                var deserialized = Expression.Call(
                                    null,
                                    deserialize,
                                    new Expression[] { parameterDictionaryVar, paramTypeConst, serializerSettingsConst });
                                convertParam = Expression.Convert(deserialized, paramType);
                            }
                        }
                    }

                    parameterAssignment = Expression.IfThen(tryGetParam, Expression.Assign(paramVar, convertParam));
                }

                parameterAssignments.Add(parameterAssignment);
            }

            // Cast the controller into its native type and invoke it to get the outgoing observable.
            if (method.ReflectedType == null)
            {
                throw new NullReferenceException("method.ReflectedType is null");
            }

            var controller = (!method.IsStatic) ? Expression.Convert(controllerParameter, method.ReflectedType) : null;
            var outgoingObservable = Expression.Call(controller, method, parameters);

            // Convert the outgoing observable into an observable of strings.
            var selectToString = typeof(Observable).GetGenericMethod(
                "Select",
                new[] { method.ReturnType, typeof(Func<,>).MakeGenericType(method.ReturnType.GenericTypeArguments[0], typeof(string)) },
                new[] { method.ReturnType.GenericTypeArguments[0], typeof(string) });
            Expression<Func<object, string>> serialize = input => JsonConvert.SerializeObject(input, serializerSettings);
            var outgoingStringObservable =
                Expression.Call(null, selectToString, new Expression[] { outgoingObservable, serialize });

            parameterAssignments.Add(outgoingStringObservable);
            allVariables.AddRange(parameters);
            var body = Expression.Block(allVariables.ToArray(), parameterAssignments);
            
            // Return the compiled lambda.
            var result = Expression.Lambda<Invoker>(body, controllerParameter, parametersParameter, getObservableParameter);
            var compiled = result.Compile();
            return compiled;
        }

        /// <summary>
        /// Returns the controller for the provided <paramref name="type"/>.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// The controller for the provided <paramref name="type"/>.
        /// </returns>
        public object GetStreamController(Type type)
        {
            return this.streamControllers.GetOrAdd(type, Activator.CreateInstance);
        }

        /// <summary>
        /// Returns the <see cref="RoutePrefixAttribute"/> value for the provided <paramref name="type"/>.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// The <see cref="RoutePrefixAttribute"/> value for the provided <paramref name="type"/>.
        /// </returns>
        public string GetRoutePrefix(Type type)
        {
            var attr = type.GetCustomAttribute<RoutePrefixAttribute>();
            return attr != null ? attr.Prefix : string.Empty;
        }

        /// <summary>
        /// Returns the <see cref="RouteAttribute.Route"/> value for the provided <paramref name="method"/>.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <returns>
        /// The <see cref="RouteAttribute.Route"/> value for the provided <paramref name="method"/>.
        /// </returns>
        public string GetRouteSuffixTemplate(MethodInfo method)
        {
            var attr = method.GetCustomAttribute<RouteAttribute>();
            return attr != null ? attr.Route : string.Empty;
        }

        /// <summary>
        /// Returns the steam controller methods for the provided <paramref name="type"/>.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// The steam controller methods for the provided <paramref name="type"/>.
        /// </returns>
        public IEnumerable<MethodInfo> GetStreamMethods(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            return methods.Where(this.IsStreamMethod);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the provided <paramref name="method"/> is a stream controller method,
        /// <see langword="false"/> otherwise.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the provided <paramref name="method"/> is a stream controller method,
        /// <see langword="false"/> otherwise.
        /// </returns>
        public bool IsStreamMethod(MethodInfo method)
        {
            var isObservable = method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(IObservable<>);
            var hasRouteTemplate = this.GetRouteSuffixTemplate(method) != null;
            return isObservable && hasRouteTemplate;
        }

        /// <summary>
        /// Returns the joined route for the provided values.
        /// </summary>
        /// <param name="prefix">
        /// The prefix.
        /// </param>
        /// <param name="template">
        /// The template.
        /// </param>
        /// <returns>
        /// The joined route for the provided values.
        /// </returns>
        public string JoinRouteParts(string prefix, string template)
        {
            if (!string.IsNullOrWhiteSpace(template))
            {
                return !string.IsNullOrWhiteSpace(prefix) ? prefix + '/' + template : template;
            }

            return prefix;
        }

        /// <summary>
        /// Returns the steam controller routes for the provided <paramref name="type"/>.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <returns>
        /// The steam controller routes for the provided <paramref name="type"/>.
        /// </returns>
        public IEnumerable<StreamRoute> GetStreamRoutes(Type type)
        {
            var prefix = this.GetRoutePrefix(type);
            var streams = this.GetStreamMethods(type).ToList();
            return
                streams
                    .Select(method => new StreamRoute { Handler = method, Route = this.JoinRouteParts(prefix, this.GetRouteSuffixTemplate(method)) });
        }

        /// <summary>
        /// Pumps incoming messages from <paramref name="socket"/> into their corresponding observables.
        /// </summary>
        /// <param name="socket">
        /// The socket.
        /// </param>
        /// <param name="incomingObservables">
        /// The incoming observables.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which completes when an error occurs, the socket closes, or .
        /// </returns>
        private static async Task SubscribeManyToSocket(WebSocket socket, Dictionary<string, ISubject<string>> incomingObservables)
        {
            while (!socket.IsClosed)
            {
                try
                {
                    var message = await socket.ReceiveString();
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        continue;
                    }

                    // Get the name of the incoming observable.
                    var nameIndex = message.IndexOf('.');
                    if (nameIndex <= 0)
                    {
                        nameIndex = message.Length;
                    }

                    var name = message.Substring(1, nameIndex - 1).ToLowerInvariant();

                    // Retrieve the named observable.
                    ISubject<string> observable;
                    if (!incomingObservables.TryGetValue(name, out observable))
                    {
                        // Observable not found, meaning that the controller method does not care about this observable.
                        continue;
                    }

                    // Route the message to the correct method.
                    switch (message[0])
                    {
                        case ResponseKind.Next:
                            {
                                // OnNext
                                var payload = message.Substring(nameIndex + 1);
                                observable.OnNext(payload);
                                break;
                            }

                        case ResponseKind.Error:
                            {
                                // OnError
                                var payload = message.Substring(nameIndex + 1);
                                observable.OnError(new Exception(payload));

                                // Remove the observable, it will never be called again.
                                incomingObservables.Remove(name);
                                break;
                            }

                        case ResponseKind.Completed:
                            {
                                // OnCompleted
                                observable.OnCompleted();

                                // Remove the observable, it will never be called again.
                                incomingObservables.Remove(name);
                                break;
                            }
                    }
                }
                catch (Exception)
                {
                    // An error occurred, exit.
                    break;
                }

                if (!incomingObservables.Any())
                {
                    // No more observables, exit.
                    break;
                }
            }

            foreach (var observable in incomingObservables.Values)
            {
                observable.OnCompleted();
            }
        }

        /// <summary>
        /// Subscribes to the provided <paramref name="outgoing"/> stream, sending all events to the provided <paramref name="socket"/>.
        /// </summary>
        /// <param name="outgoing">The outgoing stream.</param>
        /// <param name="socket">
        /// The socket.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> which will complete when either the observable completes (or errors) or the socket is closed (or errors).
        /// </returns>
        private static async Task SubscribeViaSocket(IObservable<string> outgoing, WebSocket socket)
        {
            var completion = new TaskCompletionSource<int>();
            var subscription = new IDisposable[] { null };

            Action complete = () =>
            {
                if (subscription[0] != null)
                {
                    subscription[0].Dispose();
                }

                completion.TrySetResult(0);
            };

            // Until the socket is closed, pipe messages to it, then complete.
            subscription[0] = outgoing.TakeWhile(_ => !socket.IsClosed).SelectMany(
                next => socket.Send(ResponseKind.Next + next).ToObservable(),
                error => socket.Send(ResponseKind.Error + JsonConvert.SerializeObject(error.Message)).ToObservable(),
                () =>
                {
                    if (!socket.IsClosed)
                    {
                        // Send and close the socket.
                        var send = socket.Send("c").ToObservable();
                        return send.SelectMany(_ => socket.Close((int)WebSocketCloseStatus.NormalClosure, "onCompleted").ToObservable());
                    }

                    return Observable.Empty<Unit>();
                }).Subscribe(_ => { }, _ => complete(), complete);
            await completion.Task;
        }

        /// <summary>
        /// Returns an observable from the provided <paramref name="handler"/>.
        /// </summary>
        /// <param name="handler">
        /// The handler delegate.
        /// </param>
        /// <returns>
        /// The <see cref="IObservable{T}"/>.
        /// </returns>
        private static IObservable<string> GetObservableFromHandler(Func<IObservable<string>> handler)
        {
            // Capture any exceptions from the handler so that they can be propagated.
            IObservable<string> outgoing;
            try
            {
                outgoing = handler();
            }
            catch (Exception exception)
            {
                outgoing = Observable.Throw<string>(exception);
            }

            return outgoing;
        }
    }
}