using IVSoftware.Portable.Common.Exceptions;
using IVSoftware.Portable.Disposable;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IVSoftware.Portable
{
    /// <summary>
    /// Async-aware finalize event arguments that allow epoch settlement to be awaited
    /// without altering the operational semantics of WatchdogTimer.
    /// </summary>
    /// <remarks>
    /// Provides an awaitable completion boundary for epoch finalization. If no asynchronous
    /// participation is introduced, awaiting completes immediately. When copied from another
    /// async-aware instance, existing finalization state is preserved to maintain continuity
    /// of the timer epoch.
    /// </remarks>
    public sealed class EpochFinalizingAsyncEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes finalize arguments with a settled default.
        /// </summary>
        /// <remarks>
        /// If no asynchronous participation is introduced, awaiting this instance
        /// completes immediately.
        /// </remarks>
        internal EpochFinalizingAsyncEventArgs(
            EpochFinalizationSnapshot snapshot)
        {
            IsCanceled = snapshot.IsCanceled;
            TCS = snapshot.TCS;
            UserEventArgs = snapshot.UserEventArgs;
        }

        public bool IsCanceled { get; }


        private readonly AsyncLocal<bool> _inEpochInvoke = new AsyncLocal<bool>();
        private readonly SemaphoreSlim _fifo = new SemaphoreSlim(1, 1);


        /// <summary>
        /// Use this method inside an EpochFinalizing handler to prolong the epoch
        /// with structured, ordered asynchronous work.
        /// </summary>
        /// <remarks>
        /// Quick start rules:
        /// 
        /// - Call <see cref="EpochInvokeAsync"/> exactly once per handler invocation.
        /// - Do not await (yield) before calling this method.
        /// - Place all synchronous and asynchronous work that must participate in
        ///   the epoch inside the supplied delegate.
        /// 
        /// Each call enters a FIFO queue and is awaited in order. The epoch completes
        /// only after all queued delegates have finished. Work started outside this
        /// method, or invoked after the epoch has completed, does not participate
        /// in settlement.
        /// </remarks>
        public async Task EpochInvokeAsync(Func<Task> asyncAction, TimeSpan? timeout = null)
        {
            if(TCS.Task.IsCompleted)
            {
                var msg = @"
See the Quick Start rules in the method documentation:
- Call EpochInvokeAsync before yielding.
- Participation must be declared synchronously within the handler.

EpochInvokeAsync was invoked after the epoch had already completed
(allowing any awaiting callers to resume).

If this Throw is handled, the delegate will execute,
but outside the awaited epoch boundary.
".TrimStart();

                if (this.ThrowHard<InvalidOperationException>(msg).Handled)
                {   /* G T K */
                    // Throw suppressed.
                    // Epoch has already completed.
                    // Awaiting callers have resumed.
                    // This delegate will execute but will not be awaited.
                }
            }
            timeout ??= TimeSpan.FromSeconds(10);

            bool acquired = false;
            bool reentrant = _inEpochInvoke.Value;

            try
            {
                if (reentrant)
                {
                    this.ThrowHard<InvalidOperationException>(
                        "EpochInvokeAsync cannot be called reentrantly within the same epoch.");
                }
                else
                {
                    acquired = await _fifo.WaitAsync(timeout.Value);

                    if (acquired)
                    {
                        _inEpochInvoke.Value = true;
                        await asyncAction();
                    }
                    else
                    {
                        this.ThrowHard<TimeoutException>();
                    }
                }
            }
            finally
            {
                if (acquired)
                {
                    _fifo.Release();
                }

                _inEpochInvoke.Value = false;
            }
        }
        public void EnqueueTask(Func<Task> task) => Awaitables.Enqueue(task);

        #region I N T E R N A L 
        /// <summary>
        /// Restartable thread synchronization object.
        /// </summary>
        internal TaskCompletionSource<TaskStatus> TCS { get; }
        internal EventArgs UserEventArgs { get; }

        internal Queue<Func<Task>> Awaitables { get; } = new Queue<Func<Task>>();

        #endregion I N T E R N A L
    }

    /// <summary>
    /// Internal snapshot class.
    /// </summary>
    class EpochFinalizationSnapshot
    {
        public TaskCompletionSource<TaskStatus> TCS { get; set; }
        public Action OnCompleted { get; set; }

        public EventArgs UserEventArgs { get; set; }
        public bool IsCanceled { get; set; }
    }
}
