// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   The queue subject.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Dapr.WebStream.Server
{
    using System;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading;

    /// <summary>
    /// The queue subject.
    /// </summary>
    /// <typeparam name="T">
    /// The subject value type.
    /// </typeparam>
    internal class QueueSubject<T> : ISubject<T>
    {
        /// <summary>
        /// The queue.
        /// </summary>
        private ISubject<T> queue = new ReplaySubject<T>();

        /// <summary>
        /// The completed.
        /// </summary>
        private int completed;

        /// <summary>
        /// The internal observer.
        /// </summary>
        private IObserver<T> internalObserver;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueSubject{T}"/> class.
        /// </summary>
        public QueueSubject()
        {
            this.internalObserver = this.queue;
        }

        /// <summary>
        /// Provides the observer with new data.
        /// </summary>
        /// <param name="value">The current notification information.</param>
        public void OnNext(T value)
        {
            this.internalObserver.OnNext(value);
        }

        /// <summary>
        /// Notifies the observer that the provider has experienced an error condition.
        /// </summary>
        /// <param name="error">An object that provides additional information about the error.</param>
        public void OnError(Exception error)
        {
            this.internalObserver.OnError(error);
        }

        /// <summary>
        /// Notifies the observer that the provider has finished sending push-based notifications.
        /// </summary>
        public void OnCompleted()
        {
            Interlocked.Exchange(ref this.completed, 1);
            this.internalObserver.OnCompleted();
        }

        /// <summary>
        /// Notifies the provider that an observer is to receive notifications.
        /// </summary>
        /// <returns>
        /// A reference to an interface that allows observers to stop receiving notifications before the provider has finished sending them.
        /// </returns>
        /// <param name="observer">The object that is to receive notifications.</param>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var result = this.queue.Do(
                observer.OnNext,
                observer.OnError,
                () =>
                {
                    this.internalObserver = observer;
                    this.queue = null;

                    if (this.completed == 1)
                    {
                        observer.OnCompleted();
                    }
                }).Subscribe();
            Interlocked.CompareExchange(ref this.completed, 2, 0);
            this.queue.OnCompleted();

            return result;
        }
    }
}