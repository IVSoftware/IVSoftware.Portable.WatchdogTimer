using IVSoftware.Portable.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IVSoftware.Portable
{
    /// <summary>
    /// Async-aware finalize event arguments that allow epoch settlement to be awaited
    /// without altering the operational semantics of WatchdogTimer.
    /// </summary>
    /// <remarks>
    /// - The watchdog timer provides an awaitable completion boundary for epoch finalization. 
    /// - Participation is introduced by adding async workloads to the FIFO.
    /// - Upon return from the handler, this collection is sealed and tasks are consecutively 
    ///   awaited in the order they were received.
    /// </remarks>
    public sealed class EpochFinalizingAsyncEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes finalize arguments with a settled default.
        /// </summary>
        internal EpochFinalizingAsyncEventArgs(
            EpochFinalizationSnapshot snapshot)
        {
            IsCanceled = snapshot.IsCanceled;
            TCS = snapshot.TCS;
            UserEventArgs = snapshot.UserEventArgs;
        }

        public bool IsCanceled { get; }

        /// <summary>
        /// Adds an async workload to the finalization FIFO.
        /// </summary>
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

        #region D E P R E C A T E D 
#if ABSTRACT
            // From the documentation for 1.3.1-beta
            await e.EpochInvokeAsync(async () =>
            { 
                var acnx = await _dhost.GetCnx();
                var recordset = await acnx.QueryAsync<Item>(
                    "SELECT * FROM Item WHERE Description LIKE ?",
                    $"%{InputText}%");
                Items.Clear();
                foreach (var item in recordset)
                {
                    Items.Add(item);
                }
            });
#endif

        [Obsolete("Use QueueEpochTask to register async workloads for epoch finalization.")]
        public Task EpochInvokeAsync(Func<Task> task)
        {
            string msg = @"
This method is:
- Retained for compatibility.
- Still functional - the specified task will be enqueued to the finalization FIFO.
- Superseded by a simplified model using a void-returning registration method.

If prior call sites marked the EpochFinalizing handler async only
to await EpochInvokeAsync, that await is now unnecessary.
The handler may be restored to a synchronous form, and the workload
registered via QueueEpochTask. Await the WatchdogTimer itself
for deterministic settlement.
".TrimStart();

            this.Advisory(msg);

            QueueEpochTask(task);
            return Task.CompletedTask;
        }
        #endregion D E P R E C A T E D
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
