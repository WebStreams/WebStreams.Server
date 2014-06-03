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
        /// Gets or sets the template.
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Gets or sets the target method.
        /// </summary>
        public MethodInfo Method { get; set; }
    }
}