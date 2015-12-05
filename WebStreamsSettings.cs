// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Settings for the WebStreams OWIN middleware.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebStreams.Server
{
    using System;
    using System.Collections.Concurrent;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Settings for the WebStreams OWIN middleware.
    /// </summary>
    public class WebStreamsSettings
    {
        /// <summary>
        /// Initializes static members of the <see cref="WebStreamsSettings"/> class.
        /// </summary>
        static WebStreamsSettings()
        {
            Default = new WebStreamsSettings();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebStreamsSettings"/> class.
        /// </summary>
        public WebStreamsSettings()
        {
            var defaultSerializerSettings = new JsonSerializerSettings
                                            {
                                                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                                NullValueHandling = NullValueHandling.Ignore,
                                                MissingMemberHandling = MissingMemberHandling.Ignore,
                                                DefaultValueHandling = DefaultValueHandling.Ignore,
                                            };
            defaultSerializerSettings.Converters.Add(new StringEnumConverter { CamelCaseText = true, AllowIntegerValues = true });
            var streamControllers = new ConcurrentDictionary<Type, object>();
            this.RoutePrefix = string.Empty;
            this.JsonSerializerSettings = defaultSerializerSettings;
            this.GetControllerInstanceDelegate = type => streamControllers.GetOrAdd(type, Activator.CreateInstance);
        }

        /// <summary>
        /// Gets the default settings.
        /// </summary>
        public static WebStreamsSettings Default { get; private set; }

        /// <summary>
        /// Gets or sets the delegate used to get controller instances.
        /// </summary>
        /// <remarks>
        /// Called once for each connection to get the controller instance.
        /// </remarks>
        public Func<Type, object> GetControllerInstanceDelegate { get; set; }

        /// <summary>
        /// Gets or sets the route prefix for stream controllers.
        /// </summary>
        /// <remarks>
        /// The route prefix is prepended onto the controller's RoutePrefixAttribute, if present.
        /// </remarks>
        public string RoutePrefix { get; set; }

        /// <summary>
        /// Gets or sets the JSON serializer settings.
        /// </summary>
        public JsonSerializerSettings JsonSerializerSettings { get; set; }
    }
}