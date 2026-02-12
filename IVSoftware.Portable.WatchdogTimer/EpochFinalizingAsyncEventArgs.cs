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

        public void QueueEpochTask(Func<Task> task) => EpochFinalizeQueue.Enqueue(task);

        #region I N T E R N A L 
        /// <summary>
        /// Restartable thread synchronization object.
        /// </summary>
        internal TaskCompletionSource<TaskStatus> TCS { get; }
        internal EventArgs UserEventArgs { get; }

        /// <remarks>
        /// No lock required. By the time this is accessed, the event 
        /// is offline and not subject to the vagaries of concurrency.
        /// </remarks>
        internal Queue<Func<Task>> EpochFinalizeQueue { get; } = new ();

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
