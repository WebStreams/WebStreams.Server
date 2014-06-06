// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StreamControllerManager.cs" company="Dapr Labs">
//   Copyright 2014.
// </copyright>
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
    using System.Reactive.Linq;
    using System.Reflection;

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
            var dictionaryGet = typeof(IDictionary<string, string>).GetMethod("get_Item");
            var deserialize = typeof(JsonConvert).GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type), typeof(JsonSerializerSettings) });
            var invokeFunc = typeof(Func<string, IObservable<string>>).GetMethod("Invoke");

            // Construct expressions to retrieve each of the controller method's parameters.
            var parameters = new List<Expression>();
            foreach (var parameter in method.GetParameters())
            {
                var name = parameter.Name.ToLowerInvariant();

                Expression value;
                if (parameter.ParameterType == typeof(string))
                {
                    // Strings need no conversion, just pluck the value from the parameter list.
                    value = Expression.Call(parametersParameter, dictionaryGet, new Expression[] { Expression.Constant(name) });
                }
                else if (parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() == typeof(IObservable<>))
                {
                    // This is an incoming observable, get the proxy observable to pass in.
                    var incomingObservable = Expression.Call(getObservableParameter, invokeFunc, new Expression[] { Expression.Constant(name) });

                    // Select the proxy observable into the correct shape.
                    var selectIncomingObservable = typeof(Observable).GetGenericMethod(
                        "Select",
                        new[] { typeof(IObservable<string>), typeof(Func<,>).MakeGenericType(typeof(string), parameter.ParameterType.GenericTypeArguments[0]) },
                        new[] { typeof(string), parameter.ParameterType.GenericTypeArguments[0] });
                    var next = Expression.Parameter(typeof(string), "next");
                    var observableType = parameter.ParameterType.GenericTypeArguments[0];
                    var selector =
                        Expression.Lambda(
                            Expression.Convert(
                                Expression.Call(
                                    null,
                                    deserialize,
                                    new Expression[] { next, Expression.Constant(observableType), Expression.Constant(serializerSettings) }),
                                observableType),
                            new[] { next });

                    // Pass the converted observable in for the current parameter.
                    value = Expression.Call(null, selectIncomingObservable, new Expression[] { incomingObservable, selector });
                }
                else
                {
                    var contract = JsonSerializer.Create(serializerSettings).ContractResolver.ResolveContract(parameter.ParameterType);
                    var isPrimitive = contract.GetType() == typeof(JsonPrimitiveContract);

                    // Some primitives (eg GUIDs, DateTime) are serialized as strings & need to be wrapped in quotes before deserialization.
                    if (isPrimitive && !parameter.ParameterType.IsNumericType() && parameter.ParameterType == typeof(bool))
                    {
                        // Wrap string-based primitive in quotes before deserializing.
                        var parameterValue = Expression.Call(parametersParameter, dictionaryGet, new Expression[] { Expression.Constant(name) });
                        var quotedParameterValue = Expression.Add(Expression.Add(Expression.Constant("\""), parameterValue), Expression.Constant("\""));
                        var deserialized = Expression.Call(
                            null,
                            deserialize,
                            new Expression[] { quotedParameterValue, Expression.Constant(parameter.ParameterType), Expression.Constant(serializerSettings) });
                        value = Expression.Convert(deserialized, parameter.ParameterType);
                    }
                    else
                    {
                        // Serialize raw value for non-primitive types, bools, and numeric types.
                        var parameterValue = Expression.Call(parametersParameter, dictionaryGet, new Expression[] { Expression.Constant(name) });
                        var deserialized = Expression.Call(
                            null,
                            deserialize,
                            new Expression[] { parameterValue, Expression.Constant(parameter.ParameterType), Expression.Constant(serializerSettings) });
                        value = Expression.Convert(deserialized, parameter.ParameterType);
                    }
                }

                parameters.Add(value);
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

            // Return the compiled lambda.
            return Expression.Lambda<Invoker>(outgoingStringObservable, controllerParameter, parametersParameter, getObservableParameter).Compile();
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
    }
}