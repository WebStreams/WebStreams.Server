// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Specifies that this method should be served over plain HTTP, restricting it to a single result with no protocol
//   messages.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebStreams.Server
{
    using System;

    /// <summary>
    /// Specifies that this method should be served over plain HTTP, restricting it to a single result with no protocol
    /// messages.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PlainHttpAttribute : Attribute
    {
    }
}