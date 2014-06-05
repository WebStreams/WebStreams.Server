// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RouteAttribute.cs" company="Dapr Labs">
//   Copyright 2014.
// </copyright>
// <summary>
//   Defines a route to a method.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStream.Server
{
    using System;

    /// <summary>
    /// Defines a route to a method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RouteAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RouteAttribute"/> class.
        /// </summary>
        /// <param name="route">
        /// The route.
        /// </param>
        public RouteAttribute(string route)
        {
            this.Route = route;
        }

        /// <summary>
        /// Gets or sets the route.
        /// </summary>
        public string Route { get; set; }
    }
}