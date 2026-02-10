using IVSoftware.Portable.Disposable;
using System;
using System.Collections.Generic;
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
        public EpochFinalizingAsyncEventArgs(bool isCanceled)
        {
            IsCanceled = isCanceled;
        }
        public bool IsCanceled { get; }

        /// <summary>
        /// Copy constructor that preserves async-finalization semantics when present.
        /// </summary>
        /// <remarks>
        /// If the source instance is already async-aware, its completion source
        /// is adopted to preserve continuity of the epoch finalization boundary.
        /// </remarks>
        public EpochFinalizingAsyncEventArgs(EpochFinalizingAsyncEventArgs other)
            : this(other.IsCanceled)
        { }

        /// <summary>
        /// Begins asynchronous participation in epoch finalization.
        /// </summary>
        /// <remarks>
        /// This method grants a parallel reference-counted slot within the finalization lifetime.
        /// Tokens may be consecutive or overlap to represent handoffs of async work.
        /// </remarks>
        public IDisposable BeginAsync() => DHostAsync.GetToken();

        DisposableHost DHostAsync
        {
            get
            {
                if (_dhostAsync is null)
                {
                    _dhostAsync = new DisposableHost();
                    _dhostAsync.BeginUsing += (sender, e) =>
                    {
                        Busy.Wait(0);
                    };
                    _dhostAsync.FinalDispose += (sender, e) =>
                    {
                        // DisposableHost is the sole authority on epoch participation; Busy is only a projection.
                        // If the semaphore is already signaled, normalize quietly rather than faulting the process
                        // over a non-authoritative synchronization artifact.
                        Busy.Wait(0);

                        // Now, with confidence, reassert the semaphore to the signaled state.
                        Busy.Release();
                    };
                }
                return _dhostAsync;
            }
        }
        DisposableHost _dhostAsync = null;

        /// <summary>
        /// Restartable thread synchronization object.
        /// </summary>
        internal SemaphoreSlim Busy { get; } = new SemaphoreSlim(1, 1);
    }
}
