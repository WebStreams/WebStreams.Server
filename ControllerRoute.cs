// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Describes a route on a controller.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStreams.Server
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Describes a route on a controller.
    /// </summary>
    internal class ControllerRoute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ControllerRoute"/> class.
        /// </summary>
        /// <param name="route">
        /// The route.
        /// </param>
        /// <param name="controllerType">
        /// The controller type.
        /// </param>
        /// <param name="invoker">
        /// The invoker.
        /// </param>
        /// <param name="observableParameters">
        /// The observable parameters.
        /// </param>
        /// <param name="hasBodyParameter">
        /// The value indicating whether or not this route has a parameter derived from the request body.
        /// </param>
        public ControllerRoute(string route, Type controllerType, Invoker invoker, HashSet<string> observableParameters, bool hasBodyParameter)
        {
            this.HasBodyParameter = hasBodyParameter;
            this.Route = route;
            this.ControllerType = controllerType;
            this.Invoke = invoker;
            this.ObservableParameters = observableParameters;
        }

        /// <summary>
        /// Describes a stream controller method invoker.
        /// </summary>
        /// <param name="controller">
        /// The controller.
        /// </param>
        /// <param name="parameters">
        /// The parameters dictionary.
        /// </param>
        /// <param name="getObservable">
        /// The delegate used to retrieve the <see cref="IObservable{T}"/> for a provided parameter.
        /// </param>
        /// <returns>
        /// The resulting stream.
        /// </returns>
        public delegate IObservable<string> Invoker(object controller, IDictionary<string, string> parameters, Func<string, IObservable<string>> getObservable);

        /// <summary>
        /// Gets the route.
        /// </summary>
        public string Route { get; private set; }

        /// <summary>
        /// Gets the invoker.
        /// </summary>
        public Invoker Invoke { get; private set; }

        /// <summary>
        /// Gets the incoming observables.
        /// </summary>
        public HashSet<string> ObservableParameters { get; private set; }

        /// <summary>
        /// Gets the controller type.
        /// </summary>
        public Type ControllerType { get; private set; }

        /// <summary>
        /// Gets a value indicating whether or not this route has a parameter derived from the request body.
        /// </summary>
        public bool HasBodyParameter { get; private set; }
    }
}