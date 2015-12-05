// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   OWIN extensions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebStreams.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.Owin;

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
                    var cancellationToken = environment.Request.CallCancelled;
                    ControllerRoute route;
                    if (routes.TryGetValue(path, out route))
                    {
                        var args = environment.Request.Query.ToDictionary(x => x.Key, x => Uri.UnescapeDataString(x.Value.First()));
                        var controller = settings.GetControllerInstanceDelegate(route.ControllerType);

                        // Determine if this is a WebSockets request.
                        var accept = environment.Get<Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>>(WebSocketConstants.Accept);
                        if (environment.Request.IsWebSocketRequest() && accept != null)
                        {
                            // Accept using the WebSocket handler.
                            accept(null, route.WebSocketRequestHandler(controller, args));
                        }
                        else
                        {
                            // Handle body-valued parameters.
                            if (route.HasBodyParameter)
                            {
                                using (var reader = new StreamReader(environment.Request.Body, Encoding.UTF8, false))
                                {
                                    args[ControllerBuilder.BodyParameterName] = await reader.ReadToEndAsync();
                                }
                            }

                            // Accept using the HTTP handler.
                            await route.HttpRequestHandler(controller, args, environment, cancellationToken);
                        }
                    }
                    else
                    {
                        // Allow the next handler to handle the request.
                        await next();
                    }
                });
        }

        /// <summary>
        /// Returns a value indicating whether or not this is a WebSocket request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A value indicating whether or not this is a WebSocket request.</returns>
        private static bool IsWebSocketRequest(this IOwinRequest request)
        {
            var isUpgrade = request.IsHeaderEqual("Connection", "Upgrade");
            var isWebSocket = request.IsHeaderEqual("Upgrade", "WebSocket");
            return isUpgrade & isWebSocket;
        }

        /// <summary>
        /// Returns a value indicating whether or not <paramref name="request"/>'s <paramref name="header"/> value is
        /// equal to <paramref name="value"/>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="header">The header name.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>
        /// A value indicating whether or not <paramref name="request"/>'s <paramref name="header"/> value is equal to
        /// <paramref name="value"/>.
        /// </returns>
        private static bool IsHeaderEqual(this IOwinRequest request, string header, string value)
        {
            string[] headerValues;
            return request.Headers.TryGetValue(header, out headerValues) && headerValues.Length == 1
                   && string.Equals(value, headerValues[0], StringComparison.OrdinalIgnoreCase);
        }
    }
}
