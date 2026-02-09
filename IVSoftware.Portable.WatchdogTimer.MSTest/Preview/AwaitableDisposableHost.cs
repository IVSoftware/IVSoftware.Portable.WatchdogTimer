using IVSoftware.Portable.Common.Exceptions;
using IVSoftware.Portable.Disposable;
using System.Runtime.CompilerServices;

namespace IVSoftware.Portable.MSTest.Preview
{
    /// <summary>
    /// DisposableHost variant that exposes an awaitable epoch boundary without
    /// altering the underlying reference-counted lifecycle semantics.
    /// </summary>
    /// <remarks>
    /// Awaiting an instance completes when the current usage epoch has fully
    /// settled. Subclasses may participate in finalization by overriding
    /// <see cref="OnFinalDisposeAsync"/>, allowing ordered asynchronous teardown
    /// to be expressed explicitly while preserving the synchronous contract
    /// of <see cref="DisposableHost"/>.
    /// </remarks>
    public class AwaitableDisposableHost : DisposableHost
    {
        public AwaitableDisposableHost()
        {
            _tcs =
                new TaskCompletionSource<TaskStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tcs.TrySetResult(TaskStatus.Created);
        }
        protected override void OnBeginUsing(BeginUsingEventArgs e)
        {
            _tcs.TrySetResult(TaskStatus.RanToCompletion);
            _tcs = new TaskCompletionSource<TaskStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            base.OnBeginUsing(e);
        }

        /// <remarks>
        /// Final disposal supports cooperative asynchronous finalization with
        /// user-directed escalation. Soft failures await completion and surface
        /// a faulted result, while hard failures immediately fault the epoch
        /// without awaiting cooperation. Structural disposal always proceeds.
        /// </remarks>
        protected override async void OnFinalDispose(FinalDisposeEventArgs e)
        {
            FinalDisposeAsyncEventArgs eAsync;
            e = (eAsync = new FinalDisposeAsyncEventArgs(e));
            var taskResult = TaskStatus.RanToCompletion;

            try
            {
                await OnFinalDisposeAsync(eAsync);
                await eAsync; // only await cooperative completion on soft path
            }
            catch (Exception exAny)
            {
                // Throw on the spot unless deescalated by the user.
                if (this.RethrowHard(exAny).Handled)
                {
                    taskResult = TaskStatus.Faulted;
                }
                else
                {
                    // Hard escalation: fault the epoch and do not await cooperation.
                    _tcs.SetException(exAny);
                }
            }

            base.OnFinalDispose(e);

            if (taskResult == TaskStatus.RanToCompletion)
            {
                _tcs.TrySetResult(taskResult);
            }
        }


        /// <summary>
        /// Allows subclass to await organically, rather than expressing intent using the event TCS.
        /// </summary>
        protected virtual async Task OnFinalDisposeAsync(FinalDisposeAsyncEventArgs e) { }

        protected TaskCompletionSource<TaskStatus> _tcs;
        public TaskAwaiter<TaskStatus> GetAwaiter() => _tcs.Task.GetAwaiter();
    }

    /// <summary>
    /// Async-aware final-dispose event arguments that allow finalization to be awaited
    /// without altering the structural semantics of DisposableHost.
    /// </summary>
    /// <remarks>
    /// Provides an awaitable completion boundary for final disposal. If no asynchronous
    /// work is introduced, awaiting completes immediately. When copied from another
    /// async-aware instance, existing finalization state is preserved to maintain
    /// continuity of the disposal epoch.
    /// </remarks>
    public class FinalDisposeAsyncEventArgs : FinalDisposeEventArgs
    {
        /// <summary>
        /// Initializes async-aware final-dispose arguments with a settled default.
        /// </summary>
        /// <remarks>
        /// If no asynchronous finalization is introduced, awaiting this instance
        /// completes immediately.
        /// </remarks>
        public FinalDisposeAsyncEventArgs(
            IReadOnlyCollection<object> releasedSenders,
            IReadOnlyDictionary<string, object> snapshot)
            : base(releasedSenders, snapshot)
        {
            _tcsDflt =
                new TaskCompletionSource<TaskStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tcsDflt.TrySetResult(TaskStatus.RanToCompletion);
        }

        /// <summary>
        /// Copy constructor that preserves async-finalization semantics when present.
        /// </summary>
        /// <remarks>
        /// If the source instance is already async-aware, its completion source
        /// is adopted to ensure continuity of the finalization epoch.
        /// </remarks>
        public FinalDisposeAsyncEventArgs(FinalDisposeEventArgs other)
        : this(
            releasedSenders: other.ReleasedSenders,
            snapshot: other.Keys.ToDictionary(
                key => key,
                key => other[key]))
        {
            if (other is FinalDisposeAsyncEventArgs otherAsync)
            {
                // Preserve existing async-finalization semantics
                _tcs = otherAsync.TCS;
            }
        }

        /// <summary>
        /// Gets or assigns the task completion source governing async finalization.
        /// </summary>
        /// <remarks>
        /// - The completion source is immutable once assigned and represents a new unambiguous authority.
        /// - Handlers that introduce asynchronous finalization are expected to retain the provided completion
        ///   source and signal it when teardown has concluded
        /// </remarks>
        public TaskCompletionSource<TaskStatus> TCS
        {
            get => _tcs ?? _tcsDflt;
            set
            {
                if (_tcs is null)
                {
                    _tcs = value;
                }
                else
                {
                    this.ThrowHard<InvalidOperationException>(
                        $"{nameof(TCS)} is immutable once set.");
                }
            }
        }
        TaskCompletionSource<TaskStatus>? _tcs = null!;
        TaskCompletionSource<TaskStatus> _tcsDflt = null!;
        public TaskAwaiter<TaskStatus> GetAwaiter() => TCS.Task.GetAwaiter();
    }
}
