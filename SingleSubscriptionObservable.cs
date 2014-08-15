// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   Represents an observable which can be subscribed to once, and which contains a task for the single observer.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStreams.Server
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an observable which can be subscribed to once, and which contains a task for the single observer.
    /// </summary>
    internal class SingleSubscriptionObservable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleSubscriptionObservable"/> class.
        /// </summary>
        public SingleSubscriptionObservable()
        {
            var observerTask = new TaskCompletionSource<IObserver<string>>();
            this.Observer = observerTask.Task;

            var cancellation = new CancellationTokenSource();
            this.Subscription = cancellation.Token;

            this.Observable = System.Reactive.Linq.Observable.Create<string>(
                observer =>
                {
                    observerTask.SetResult(observer);
                    return cancellation.Cancel;
                });
        }

        /// <summary>
        /// Gets the observable.
        /// </summary>
        public IObservable<string> Observable { get; private set; }

        /// <summary>
        /// Gets the observer.
        /// </summary>
        public Task<IObserver<string>> Observer { get; private set; }

        /// <summary>
        /// Gets the token which is cancelled when the observer's subscription is disposed.
        /// </summary>
        public CancellationToken Subscription { get; private set; }
    }
}