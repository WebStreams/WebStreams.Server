// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Specifies that this parameter should be deserialized from the HTTP request body.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStreams.Server
{
    using System;

    /// <summary>
    /// Specifies that this parameter should be deserialized from the HTTP request body.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class BodyAttribute : Attribute
    {
    }
}