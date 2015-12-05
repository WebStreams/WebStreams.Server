// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Attribute for denoting a stream controller.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebStreams.Server
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