// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Extensions to <see cref="ControllerRoute" />.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStreams.Server
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Reactive;
    using System.Reactive.Linq;
    using System.Reactive.Threading.Tasks;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    /// <summary>
    /// Extensions to <see cref="ControllerRoute"/>.
    /// </summary>
    internal static class ControllerRouteExtensions
    {
        /// <summary>
        /// Returns a handler for incoming Web stream requests.
        /// </summary>
        /// <param name="route">
        /// The handler.
        /// </param>
        /// <param name="controller">
        /// The controller.
        /// </param>
        /// <param name="args">
        /// The request parameters.
        /// </param>
        /// <returns>
        /// A handler for incoming Web stream requests.
        /// </returns>
        public static Func<IDictionary<string, object>, Task> GetRequestHandler(
            this ControllerRoute route,
            object controller,
            IDictionary<string, string> args)
        {
            return async environment =>
            {
                using (var socket = new WebSocket(environment))
                {
                    Task incomingMessagePump;
                    Func<string, IObservable<string>> getObservable;
                    if (route.ObservableParameters != null)
                    {
                        // Route has observable parameters.
                        var observableParams = route.ObservableParameters.ToDictionary(_ => _, _ => new SingleSubscriptionObservable());
                        getObservable = name => observableParams[name].Observable;
                        incomingMessagePump = IncomingMessagePump(socket, observableParams);
                    }
                    else
                    {
                        // No observable parameters.
                        getObservable = _ => Observable.Empty<string>();
                        incomingMessagePump = Task.FromResult(0);
                    }

                    // Hook up the incoming and outgoing message pumps.
                    var outgoing = GetObservableFromHandler(() => route.Invoke(controller, args, getObservable));
                    var outgoingMessagePump = OutgoingMessagePump(outgoing, socket);

                    // Close the socket when both pumps finish.
                    await Task.WhenAll(outgoingMessagePump, incomingMessagePump);
                }
            };
        }

        /// <summary>
        /// Pumps incoming messages from <paramref name="socket"/> into their corresponding observables.
        /// </summary>
        /// <param name="socket">
        /// The socket.
        /// </param>
        /// <param name="observableParams">
        /// The incoming observables.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which completes when an error occurs, the socket closes, or .
        /// </returns>
        private static async Task IncomingMessagePump(WebSocket socket, IDictionary<string, SingleSubscriptionObservable> observableParams)
        {
            while (!socket.IsClosed)
            {
                try
                {
                    var message = await socket.ReceiveString();
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        continue;
                    }

                    // Get the name of the incoming observable.
                    var nameIndex = message.IndexOf('.');
                    if (nameIndex <= 0)
                    {
                        nameIndex = message.Length;
                    }

                    var name = message.Substring(1, nameIndex - 1);

                    // Retrieve the named observable.
                    SingleSubscriptionObservable subscription;
                    if (!observableParams.TryGetValue(name, out subscription) || subscription.Subscription.IsCancellationRequested)
                    {
                        // Observable not found, meaning that the controller method does not care about this observable.
                        continue;
                    }

                    var observer = await subscription.Observer;

                    // Route the message to the correct method.
                    switch (message[0])
                    {
                        case ResponseKind.Next:
                            {
                                var payload = message.Substring(nameIndex + 1);
                                observer.OnNext(payload);
                                break;
                            }

                        case ResponseKind.Error:
                            {
                                var payload = message.Substring(nameIndex + 1);
                                observer.OnError(new Exception(payload));

                                // Remove the observable, it will never be called again.
                                observableParams.Remove(name);
                                break;
                            }

                        case ResponseKind.Completed:
                            {
                                observer.OnCompleted();

                                // Remove the observable, it will never be called again.
                                observableParams.Remove(name);
                                break;
                            }
                    }
                }
                catch (Exception)
                {
                    // An error occurred, exit.
                    break;
                }

                if (!observableParams.Any())
                {
                    // No more observables, exit.
                    break;
                }
            }

            foreach (var subscription in observableParams.Values.Where(subscription => !subscription.Subscription.IsCancellationRequested))
            {
                (await subscription.Observer).OnCompleted();
            }
        }

        /// <summary>
        /// Subscribes to the provided <paramref name="outgoing"/> stream, sending all events to the provided <paramref name="socket"/>.
        /// </summary>
        /// <param name="outgoing">The outgoing stream.</param>
        /// <param name="socket">
        /// The socket.
        /// </param>
        /// <returns>
        /// The <see cref="Task"/> which will complete when either the observable completes (or errors) or the socket is closed (or errors).
        /// </returns>
        private static async Task OutgoingMessagePump(IObservable<string> outgoing, WebSocket socket)
        {
            var completion = new TaskCompletionSource<int>();
            var subscription = new IDisposable[] { null };

            Action complete = () =>
            {
                if (subscription[0] != null)
                {
                    subscription[0].Dispose();
                }

                completion.TrySetResult(0);
            };

            // Until the socket is closed, pipe messages to it, then complete.
            subscription[0] = outgoing.TakeWhile(_ => !socket.IsClosed).SelectMany(
                next => socket.Send(ResponseKind.Next + next).ToObservable(),
                error => socket.Send(ResponseKind.Error + JsonConvert.SerializeObject(error.Message)).ToObservable(),
                () =>
                {
                    if (!socket.IsClosed)
                    {
                        // Send and close the socket.
                        var send = socket.Send(ResponseKind.Completed.ToString(CultureInfo.InvariantCulture)).ToObservable();
                        return send.SelectMany(_ => socket.Close((int)WebSocketCloseStatus.NormalClosure, "onCompleted").ToObservable());
                    }

                    return Observable.Empty<Unit>();
                }).Subscribe(_ => { }, _ => complete(), complete);
            await completion.Task;
        }

        /// <summary>
        /// Returns an observable from the provided <paramref name="handler"/>.
        /// </summary>
        /// <param name="handler">
        /// The handler delegate.
        /// </param>
        /// <returns>
        /// The <see cref="IObservable{T}"/>.
        /// </returns>
        private static IObservable<string> GetObservableFromHandler(Func<IObservable<string>> handler)
        {
            // Capture any exceptions from the handler so that they can be propagated.
            IObservable<string> outgoing;
            try
            {
                outgoing = handler();
            }
            catch (Exception exception)
            {
                outgoing = Observable.Throw<string>(exception);
            }

            return outgoing;
        }
    }
}
