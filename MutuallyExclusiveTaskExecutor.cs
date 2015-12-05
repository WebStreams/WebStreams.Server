// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   The mutually exclusive task executor.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebStreams.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The mutually exclusive task executor.
    /// </summary>
    internal class MutuallyExclusiveTaskExecutor : IDisposable
    {
        /// <summary>
        /// The tasks.
        /// </summary>
        private readonly ConcurrentQueue<Func<Task>> tasks = new ConcurrentQueue<Func<Task>>();

        /// <summary>
        /// The semaphore.
        /// </summary>
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);

        private bool completed;

        /// <summary>
        /// Enqueues the provided <paramref name="action"/> for execution.
        /// </summary>
        /// <param name="action">
        /// The action being invoked.
        /// </param>
        public void Schedule(Func<Task> action)
        {
            this.tasks.Enqueue(action);
            this.semaphore.Release();
        }

        public void Complete()
        {
            this.tasks.Enqueue(
                () =>
                {
                    this.completed = true;
                    return Task.FromResult(0);
                });
        }

        /// <summary>
        /// Invokes the executor.
        /// </summary>
        /// <param name="cancellationToken">The cancellation task.</param>
        /// <returns>A <see cref="Task"/> representing the work performed.</returns>
        public async Task Run(CancellationToken cancellationToken)
        {
            var cancelled = cancellationToken.IsCancellationRequested || this.completed;
            while (!cancelled)
            {
                await this.semaphore.WaitAsync(cancellationToken);
                
                // Process all available items in the queue.
                Func<Task> task;
                while (this.tasks.TryDequeue(out task))
                {
                    // Execute the task we pulled out of the queue
                    await task();
                }

                cancelled = cancellationToken.IsCancellationRequested || this.completed;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (this.semaphore != null)
            {
                this.semaphore.Dispose();
            }
        }
    }
}