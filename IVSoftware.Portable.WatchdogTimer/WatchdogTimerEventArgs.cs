using IVSoftware.Portable.Disposable;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
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
        {
            if (other is not null && other._tcs is not null)
            {
                _tcs = other._tcs;
            }
        }

        /// <summary>
        /// Gets or assigns the task completion source governing epoch finalization.
        /// </summary>
        /// <remarks>
        /// The completion source is immutable once assigned and represents a single,
        /// unambiguous authority for epoch settlement.
        /// </remarks>
        TaskCompletionSource<TaskStatus> TCS
        {
            get => _tcs ?? TCSDflt;
            set
            {
                if (_tcs is null)
                {
                    _tcs = value;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"{nameof(TCS)} is immutable once set.");
                }
            }
        }

        TaskCompletionSource<TaskStatus> _tcs;

        TaskCompletionSource<TaskStatus> TCSDflt
        {
            get
            {
                if (_tcsDflt is null)
                {
                    _tcsDflt = new TaskCompletionSource<TaskStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _tcsDflt.TrySetResult(TaskStatus.RanToCompletion);
                }
                return _tcsDflt;
            }
        }
        TaskCompletionSource<TaskStatus> _tcsDflt = null;

        /// <summary>
        /// Enables awaiting epoch finalization directly on the event args.
        /// </summary>
        public TaskAwaiter<TaskStatus> GetAwaiter()
            => TCS.Task.GetAwaiter();

        /// <summary>
        /// Begins asynchronous participation in epoch finalization.
        /// </summary>
        /// <remarks>
        /// This method grants a one-time entry into the finalization lifetime.
        /// Tokens may overlap to represent handoffs of async work, but once all
        /// issued tokens have been disposed, the async cycle cannot be reentered
        /// for the current epoch.
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
                        TCS = new TaskCompletionSource<TaskStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
                    };
                    _dhostAsync.FinalDispose += (sender, e) =>
                    {
                        TCS.TrySetResult(TaskStatus.RanToCompletion);
                    };
                }
                return _dhostAsync;
            }
        }
        DisposableHost _dhostAsync = null;
    }
}
