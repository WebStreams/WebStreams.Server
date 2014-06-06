// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StreamControllerAttribute.cs" company="Dapr Labs">
//   Copyright 2014.
// </copyright>
// <summary>
//   Attribute for denoting a stream controller.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStream.Server
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