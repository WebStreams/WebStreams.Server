// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WebSocketMessageType.cs" company="Dapr Labs">
//   Copyright 2014.
// </copyright>
// <summary>
//   WebSocket message type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStream.Server
{
    using System;

    /// <summary>
    /// WebSocket message type.
    /// </summary>
    [Flags]
    internal enum WebSocketMessageType
    {
        /// <summary>
        /// A text message.
        /// </summary>
        Text = 0x1,

        /// <summary>
        /// A binary message.
        /// </summary>
        Binary = 0x2,

        /// <summary>
        /// A Close message.
        /// </summary>
        Close = 0x8
    }
}