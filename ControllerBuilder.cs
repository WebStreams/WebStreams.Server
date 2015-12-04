// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Methods for constructing controllers.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStreams.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reactive.Linq;
    using System.Reflection;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    
    /// <summary>
    /// Methods for constructing controllers
    /// </summary>
    internal static class ControllerBuilder
    {
        /// <summary>
        /// The body parameter name.
        /// </summary>
        public const string BodyParameterName = "$body";

        /// <summary>
        /// Returns the steam controller routes for the provided <paramref name="controller"/>.
        /// </summary>
        /// <param name="controller">
        /// The controller type.
        /// </param>
        /// <param name="settings">
        /// The settings.
        /// </param>
        /// <returns>
        /// The steam controller routes for the provided <paramref name="controller"/>.
        /// </returns>
        public static IDictionary<string, ControllerRoute> GetRoutes(Type controller, WebStreamsSettings settings)
        {
            var methodRoutePrefix = JoinRouteParts(settings.RoutePrefix, GetRoutePrefix(controller));
            return GetStreamMethods(controller)
                .Select(method => GetControllerMethod(method, settings, methodRoutePrefix))
                .ToDictionary(_ => _.Route, _ => _);
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
        private static string GetRoutePrefix(Type type)
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
        private static string GetRouteSuffixTemplate(MethodInfo method)
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
        private static IEnumerable<MethodInfo> GetStreamMethods(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            return methods.Where(IsStreamMethod);
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
        private static bool IsStreamMethod(MethodInfo method)
        {
            var isObservable = method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(IObservable<>);
            var hasRouteTemplate = GetRouteSuffixTemplate(method) != null;
            return isObservable && hasRouteTemplate;
        }

        /// <summary>
        /// Returns the joined route for the provided values.
        /// </summary>
        /// <param name="parts">
        /// The route parts.
        /// </param>
        /// <returns>
        /// The joined route for the provided values.
        /// </returns>
        private static string JoinRouteParts(params string[] parts)
        {
            return '/' + string.Join("/", parts.Where(_ => !string.IsNullOrWhiteSpace(_)).Select(_ => _.Trim('/')));
        }

        /// <summary>
        /// Returns the <see cref="ControllerRoute.Invoker"/> for the provided <paramref name="method"/>.
        /// </summary>
        /// <param name="method">
        /// The method.
        /// </param>
        /// <param name="settings">
        /// The settings.
        /// </param>
        /// <param name="methodRoutePrefix">
        /// The route prefix for the controller.
        /// </param>
        /// <returns>
        /// The <see cref="ControllerRoute"/> for the provided <paramref name="method"/>.
        /// </returns>
        private static ControllerRoute GetControllerMethod(MethodInfo method, WebStreamsSettings settings, string methodRoutePrefix)
        {
            var observableParameters = new HashSet<string>();
            var hasBodyParameter = false;

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
                // Resulting parameter value.
                var paramType = parameter.ParameterType;
                var paramVar = Expression.Variable(paramType, parameter.Name);
                parameters.Add(paramVar);
                Expression parameterAssignment;

                // Check if this parameter is derived from the request body.
                var isBody = parameter.GetCustomAttribute<BodyAttribute>() != null;
                if (isBody)
                {
                    if (hasBodyParameter)
                    {
                        throw new InvalidAttributeUsageException(
                            "BodyAttribute cannot be used with multiple parameters.");
                    }

                    hasBodyParameter = true;
                }

                // If the parameter is an observable, get the incoming stream using the "getObservable" parameter.
                var serializerSettingsConst = Expression.Constant(settings.JsonSerializerSettings);
                if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(IObservable<>))
                {
                    if (isBody)
                    {
                        throw new InvalidAttributeUsageException("BodyAttribute cannot be used with observable parameters.");
                    }

                    // Add the observable parameter to the stream controller definition.
                    observableParameters.Add(parameter.Name);

                    // This is an incoming observable, get the proxy observable to pass in.
                    var incomingObservable = Expression.Call(getObservableParameter, invokeFunc, new Expression[] { Expression.Constant(parameter.Name) });

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
                                Expression.Call(null, deserialize, new Expression[] { next, Expression.Constant(observableType), serializerSettingsConst }),
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
                    var paramName = isBody ? BodyParameterName : parameter.Name;
                    Expression convertParam;
                    var tryGetParam = Expression.Call(
                        parametersParameter,
                        tryGetValue,
                        new Expression[] { Expression.Constant(paramName), parameterDictionaryVar });

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
                            var contract = JsonSerializer.Create(settings.JsonSerializerSettings).ContractResolver.ResolveContract(paramType);
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
            Expression<Func<object, string>> serialize = input => JsonConvert.SerializeObject(input, settings.JsonSerializerSettings);
            var outgoingStringObservable = Expression.Call(null, selectToString, new Expression[] { outgoingObservable, serialize });

            parameterAssignments.Add(outgoingStringObservable);
            allVariables.AddRange(parameters);
            var body = Expression.Block(allVariables.ToArray(), parameterAssignments);

            var lambda = Expression.Lambda<ControllerRoute.Invoker>(body, controllerParameter, parametersParameter, getObservableParameter);
            var route = JoinRouteParts(methodRoutePrefix, GetRouteSuffixTemplate(method));
            return new ControllerRoute(route, method.DeclaringType, lambda.Compile(), observableParameters, hasBodyParameter);
        }
    }
}