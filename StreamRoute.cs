// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StreamRoute.cs" company="">
//   
// </copyright>
// <summary>
//   Represents a routable stream controller method.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebStream
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