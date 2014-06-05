// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StreamRoute.cs" company="Dapr Labs">
//   Copyright 2014.
// </copyright>
// <summary>
//   Represents a routable stream controller method.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStream.Server
{
    using System.Reflection;

    /// <summary>
    /// Represents a routable stream controller method.
    /// </summary>
    internal class StreamRoute
    {
        /// <summary>
        /// Gets or sets the route.
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// Gets or sets the handler method.
        /// </summary>
        public MethodInfo Handler { get; set; }
    }
}