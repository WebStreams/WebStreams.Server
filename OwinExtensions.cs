// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   OWIN extensions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStreams.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using Owin;

    /// <summary>
    /// OWIN extensions.
    /// </summary>
    public static class OwinExtensions
    {
        /// <summary>
        /// Use the WebStream middleware for the provided controller type.
        /// </summary>
        /// <param name="app">
        /// The app.
        /// </param>
        /// <param name="settings">
        /// The settings, or <see langword="null"/> to use <see cref="WebStreamsSettings.Default"/>.
        /// </param>
        /// <typeparam name="T">
        /// The stream controller type.
        /// </typeparam>
        public static void UseWebStreams<T>(this IAppBuilder app, WebStreamsSettings settings = null)
        {
            app.UseWebStreams(typeof(T), settings);
        }

        /// <summary>
        /// Use the WebStreams middleware for all loaded stream controller types.
        /// </summary>
        /// <param name="app">
        /// The app.
        /// </param>
        /// <param name="settings">
        /// The settings, or <see langword="null"/> to use <see cref="WebStreamsSettings.Default"/>.
        /// </param>
        public static void UseWebStreams(this IAppBuilder app, WebStreamsSettings settings = null)
        {
            settings = settings ?? WebStreamsSettings.Default;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var types = assemblies.SelectMany(_ => _.GetTypes());
            var controllers = types.Where(
                _ => _.GetCustomAttribute<RoutePrefixAttribute>() != null || _.GetCustomAttribute<StreamControllerAttribute>() != null);

            var routes = controllers.SelectMany(controller => ControllerBuilder.GetRoutes(controller, settings));
            app.UseWebStreams(settings, routes.ToDictionary(_ => _.Key, _ => _.Value));
        }

        /// <summary>
        /// Use the WebStreams middleware for the provided controller type.
        /// </summary>
        /// <param name="app">
        /// The app.
        /// </param>
        /// <param name="controllerType">
        /// The stream controller type.
        /// </param>
        /// <param name="settings">
        /// The settings, or <see langword="null"/> to use <see cref="WebStreamsSettings.Default"/>.
        /// </param>
        private static void UseWebStreams(this IAppBuilder app, Type controllerType, WebStreamsSettings settings = null)
        {
            settings = settings ?? WebStreamsSettings.Default;
            app.UseWebStreams(settings, ControllerBuilder.GetRoutes(controllerType, settings));
        }

        /// <summary>
        /// Use the WebStreams middleware for the provided controller methods.
        /// </summary>
        /// <param name="app">
        /// The app.
        /// </param>
        /// <param name="settings">
        /// The settings.
        /// </param>
        /// <param name="routes">
        /// The controller routes.
        /// </param>
        private static void UseWebStreams(this IAppBuilder app, WebStreamsSettings settings, IDictionary<string, ControllerRoute> routes)
        {
            app.Use(
                async (environment, next) =>
                {
                    var path = environment.Request.Uri.AbsolutePath;

                    var accept = environment.Get<Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>>(WebSocketConstants.Accept);
                    ControllerRoute route;
                    if (accept != null && routes.TryGetValue(path, out route))
                    {
                        // Accept the socket.
                        var args = environment.Request.Query.ToDictionary(x => x.Key, x => x.Value.FirstOrDefault());
                        var controller = settings.GetControllerInstanceDelegate(route.ControllerType);
                        accept(null, route.GetRequestHandler(controller, args));
                    }
                    else
                    {
                        // Allow the next handler to handle the request.
                        await next();
                    }
                });
        }
    }
}
