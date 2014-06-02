namespace WebStream
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// The WebSocket OWIN constants.
    /// </summary>
    /// <remarks>
    /// See: http://owin.org/extensions/owin-WebSocket-Extension-v0.4.0.htm
    /// </remarks>
    internal static class WebSocketConstants
    {
        /// <summary>
        /// The WebSocket Accept method OWIN environment key.
        /// </summary>
        public const string Accept = "websocket.Accept";

        /// <summary>
        /// The WebSocket SendAsync OWIN environment key.
        /// </summary>
        public const string SendAsync = "websocket.SendAsync";

        /// <summary>
        /// The WebSocket ReceiveAsync OWIN environment key.
        /// </summary>
        public const string ReceiveAsync = "websocket.ReceiveAsync";

        /// <summary>
        /// The WebSocket CloseAsync OWIN environment key.
        /// </summary>
        public const string CloseAsync = "websocket.CloseAsync";

        /// <summary>
        /// The WebSocket CallCancelled OWIN environment key.
        /// </summary>
        public const string CallCancelled = "websocket.CallCancelled";

        /// <summary>
        /// The WebSocket ClientCloseStatus OWIN environment key.
        /// </summary>
        public const string ClientCloseStatus = "websocket.ClientCloseStatus";

        /// <summary>
        /// The WebSocket ClientCloseDescription OWIN environment key.
        /// </summary>
        public const string ClientCloseDescription = "websocket.ClientCloseDescription";
    }
}