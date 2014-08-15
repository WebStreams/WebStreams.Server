// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Attribute for denoting a stream controller.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStreams.Server
{
    using System;

    /// <summary>
    /// Attribute for denoting a stream controller.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class StreamControllerAttribute : Attribute
    {
    }
}