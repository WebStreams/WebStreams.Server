// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   The web socket context.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebStreams.Server
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The web socket context.
    /// </summary>
    internal class WebSocket : IDisposable
    {
        /// <summary>
        /// The function used to send data.
        /// </summary>
        private readonly Func<ArraySegment<byte>, int, bool, CancellationToken, Task> sendAsync;

        /// <summary>
        /// The delegate used to receive data.
        /// </summary>
        private readonly Func<ArraySegment<byte>, CancellationToken, Task<Tuple<int, bool, int>>> receiveAsync;

        /// <summary>
        /// The close async.
        /// </summary>
        private readonly Func<int, string, CancellationToken, Task> closeAsync;

        /// <summary>
        /// The context.
        /// </summary>
        private readonly IDictionary<string, object> context;

        /// <summary>
        /// A CancellationToken provided by the server to signal that the WebSocket has been canceled/aborted.
        /// </summary>
        private readonly CancellationToken callCancelled;

        /// <summary>
        /// A value indicating whether this instance is disposed.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocket"/> class.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        public WebSocket(IDictionary<string, object> context)
        {
            this.context = context;
            this.sendAsync = (Func<ArraySegment<byte>, int, bool, CancellationToken, Task>)context[WebSocketConstants.SendAsync];
            this.receiveAsync = (Func<ArraySegment<byte>, CancellationToken, Task<Tuple<int, bool, int>>>)context[WebSocketConstants.ReceiveAsync];
            this.closeAsync = (Func<int, string, CancellationToken, Task>)context[WebSocketConstants.CloseAsync];
            this.callCancelled = (CancellationToken)context[WebSocketConstants.CallCancelled];
        }

        /// <summary>
        /// Gets the context.
        /// </summary>
        public IDictionary<string, object> Context
        {
            get
            {
                return this.context;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the connection is closed.
        /// </summary>
        public bool IsClosed
        {
            get
            {
                object status;
                return this.disposed || (this.context.TryGetValue(WebSocketConstants.ClientCloseStatus, out status) && (int)status != 0);
            }
        }

        /// <summary>
        /// Gets the client close status.
        /// </summary>
        public int ClientCloseStatus
        {
            get
            {
                return (int)this.context[WebSocketConstants.ClientCloseStatus];
            }
        }

        /// <summary>
        /// Gets the client close description.
        /// </summary>
        public string ClientCloseDescription
        {
            get
            {
                return (string)this.context[WebSocketConstants.ClientCloseDescription];
            }
        }

        /// <summary>
        /// Send the provided <paramref name="data"/> to the client.
        /// </summary>
        /// <param name="data">
        /// The data.
        /// </param>
        /// <param name="messageType">
        /// The message type.
        /// </param>
        /// <param name="endOfMessage">
        /// A value indicating whether this is the end of the message.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task Send(ArraySegment<byte> data, int messageType, bool endOfMessage)
        {
            this.ThrowIfDisposed();
            await this.sendAsync(data, messageType, endOfMessage, this.callCancelled);
        }

        /// <summary>
        /// Send the provided <paramref name="message"/> to the client.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task Send(string message)
        {
            this.ThrowIfDisposed();
            var bytes = Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(bytes);
            await this.Send(buffer, (int)WebSocketMessageType.Text, true);
        }

        /// <summary>
        /// Receives and returns a string to the client.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task<string> ReceiveString()
        {
            this.ThrowIfDisposed();
            var result = new StringBuilder();
            var buffer = new ArraySegment<byte>(new byte[4096]);
            var message = await this.receiveAsync(buffer, this.callCancelled);
            result.Append(Encoding.UTF8.GetString(buffer.Array, 0, message.Item3));
            while (!message.Item2)
            {
                message = await this.receiveAsync(buffer, this.callCancelled);
                result.Append(Encoding.UTF8.GetString(buffer.Array, 0, message.Item3));
            }

            return result.ToString();
        }

        /// <summary>
        /// Close the connection with the client.
        /// </summary>
        /// <param name="closeStatus">
        /// The close status.
        /// </param>
        /// <param name="closeDescription">
        /// The close description.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public async Task Close(int closeStatus, string closeDescription)
        {
            this.ThrowIfDisposed();
            await this.closeAsync(closeStatus, closeDescription, this.callCancelled);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.disposed = true;
        }

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if this instance has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException("This instance has been disposed.");
            }
        }
    }
}