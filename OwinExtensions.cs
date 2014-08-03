// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   OWIN extensions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStream.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    using Owin;

    /// <summary>
    /// OWIN extensions.
    /// </summary>
    public static class OwinExtensions
    {
        /// <summary>
        /// Initializes static members of the <see cref="OwinExtensions"/> class.
        /// </summary>
        static OwinExtensions()
        {
            DefaultSerializerSettings = new JsonSerializerSettings
                                        {
                                            ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                            NullValueHandling = NullValueHandling.Ignore,
                                            MissingMemberHandling = MissingMemberHandling.Ignore,
                                            DefaultValueHandling = DefaultValueHandling.Ignore,
                                        };
            DefaultSerializerSettings.Converters.Add(new StringEnumConverter { CamelCaseText = true, AllowIntegerValues = true });
        }

        /// <summary>
        /// Gets or sets the default serializer settings.
        /// </summary>
        public static JsonSerializerSettings DefaultSerializerSettings { get; set; }

        /// <summary>
        /// Use the WebStream middleware for the provided controller type.
        /// </summary>
        /// <param name="app">
        /// The app.
        /// </param>
        /// <param name="serializerSettings">
        /// The serializer settings, or <see langword="null"/> to use <see cref="DefaultSerializerSettings"/>.
        /// </param>
        /// <typeparam name="T">
        /// The stream controller type.
        /// </typeparam>
        public static void UseWebStream<T>(this IAppBuilder app, JsonSerializerSettings serializerSettings = null)
        {
            app.UseWebStream(typeof(T), serializerSettings);
        }

        /// <summary>
        /// Use the WebStream middleware for all loaded stream controller types.
        /// </summary>
        /// <param name="app">
        /// The app.
        /// </param>
        /// <param name="serializerSettings">
        /// The serializer settings, or <see langword="null"/> to use <see cref="DefaultSerializerSettings"/>.
        /// </param>
        public static void UseWebStream(this IAppBuilder app, JsonSerializerSettings serializerSettings = null)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var types =
                assemblies.SelectMany(_ => _.GetTypes());
            var controllers = types.Where(
                _ => _.GetCustomAttribute<RoutePrefixAttribute>() != null || _.GetCustomAttribute<StreamControllerAttribute>() != null);
            var builder = new StreamControllerManager();
            foreach (var controller in controllers)
            {
                app.UseWebStream(controller, serializerSettings, builder);
            }
        }

        /// <summary>
        /// Use the WebStream middleware for the provided controller type.
        /// </summary>
        /// <param name="app">
        /// The app.
        /// </param>
        /// <param name="controllerType">
        /// The stream controller type.
        /// </param>
        /// <param name="serializerSettings">
        /// The serializer settings, or <see langword="null"/> to use <see cref="DefaultSerializerSettings"/>.
        /// </param>
        public static void UseWebStream(this IAppBuilder app, Type controllerType, JsonSerializerSettings serializerSettings)
        {
            var builder = new StreamControllerManager();
            app.UseWebStream(controllerType, serializerSettings, builder);
        }

        /// <summary>
        /// Use the WebStream middleware for the provided controller type.
        /// </summary>
        /// <param name="app">
        /// The app.
        /// </param>
        /// <param name="controllerType">
        /// The stream controller type.
        /// </param>
        /// <param name="serializerSettings">
        /// The serializer settings, or <see langword="null"/> to use <see cref="DefaultSerializerSettings"/>.
        /// </param>
        /// <param name="builder">
        /// The builder.
        /// </param>
        private static void UseWebStream(this IAppBuilder app, Type controllerType, JsonSerializerSettings serializerSettings, StreamControllerManager builder)
        {
            serializerSettings = serializerSettings ?? DefaultSerializerSettings;

            var routes = builder.GetStreamRoutes(controllerType);

            var controller = builder.GetStreamController(controllerType);
            var invokers = routes.ToDictionary(r => r.Route, r => builder.GetInvoker(r.Handler, serializerSettings));
            app.Use(
                async (ctx, next) =>
                {
                    var path = ctx.Request.Uri.AbsolutePath;

                    var accept = ctx.Get<Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>>(WebSocketConstants.Accept);
                    if (accept != null)
                    {
                        var route = invokers.FirstOrDefault(r => path == r.Key);
                        if (route.Value != null)
                        {
                            // Accept the socket.
                            var args = ctx.Request.Query.ToDictionary(x => x.Key, x => x.Value.FirstOrDefault());
                            accept(null, StreamControllerManager.CreateStreamHandler(route.Value, controller, args));
                        }
                        else
                        {
                            await next();
                        }
                    }
                    else
                    {
                        await next();
                    }
                });
        }
    }
}
