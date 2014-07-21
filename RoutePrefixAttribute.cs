// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Attribute for defining a routing prefix for all routes within a type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStream.Server
{
    using System;

    /// <summary>
    /// Attribute for defining a routing prefix for all routes within a type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RoutePrefixAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoutePrefixAttribute"/> class.
        /// </summary>
        /// <param name="prefix">
        /// The prefix.
        /// </param>
        public RoutePrefixAttribute(string prefix)
        {
            this.Prefix = prefix;
        }

        /// <summary>
        /// Gets or sets the prefix.
        /// </summary>
        public string Prefix { get; set; }
    }
}