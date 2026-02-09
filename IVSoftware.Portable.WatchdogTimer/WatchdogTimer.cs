using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace IVSoftware.Portable
{
    public class WatchdogTimer : INotifyPropertyChanged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WatchdogTimer"/> class with optional default actions for the initial and completion phases.
        /// </summary>
        /// <param name="defaultInitialAction">An optional default action to be executed when the timer starts, if no other initial action is provided in the call to the `StartOrRestart` method.</param>
        /// <param name="defaultCompleteAction">An optional default action to be executed upon successful completion of the timer, if no other completion action is provided in the call to the `StartOrRestart` method.</param>
        /// <remarks>
        /// The preferred usage is to choose one of the following approaches:
        /// - Always use default actions, or
        /// - Always use actions passed in as arguments to the method.
        /// However, in situations where both defaults and method arguments are provided, an orderly scheme is in place for resolving conflicts: actions passed as arguments to the method will always take precedence over default actions, even if defaults are set.
        /// This ensures the timer behaves predictably and consistently in scenarios where both default and explicit actions are provided.
        /// </remarks>
        public WatchdogTimer(Action defaultInitialAction = null, Action defaultCompleteAction = null)
        {
            DefaultInitialAction = defaultInitialAction;
            DefaultCompleteAction = defaultCompleteAction;
            InitializeEpoch();
            EpochTaskCompletionSource.TrySetResult(TaskStatus.Created);
        }

        /// <summary>
        /// Gets the default action to be executed when the timer starts, if no other initial action is provided.
        /// This property is read-only and can only be set through the constructor.
        /// </summary>
        private Action DefaultInitialAction { get; }

        /// <summary>
        /// Gets the default action to be executed upon successful completion of the timer, if no other completion action is provided.
        /// This property is read-only and can only be set through the constructor.
        /// </summary>
        private Action DefaultCompleteAction { get; }

        #region U S E R    E P O C H    V A L U E S 
        /// <summary>
        /// Gets the most recent non-null user-specified action to be executed upon successful completion of the timer.
        /// </summary>
        protected Action CompleteAction { get; set; }

        /// <summary>
        /// Gets the most recent user-specified event args to be supplied with completion
        /// notifications. A value of <see cref="EventArgs.Empty"/> (or null) does not
        /// overwrite the current epoch value.
        /// </summary>
        protected EventArgs UserEventArgs { get; set; }

        #endregion U S E R    E P O C H    V A L U E S


        /// <summary>
        /// These fields enable efficient restarts and cancellations of the timer without the need to cancel or manage running <see cref="Task"/> objects directly. 
        /// <para>
        /// The <c>_startCount</c> field tracks the number of times the timer has been restarted, and the <c>_cancelCount</c> field tracks the number of times it has been cancelled. 
        /// Each time the timer is restarted or cancelled, these counters are incremented. When the <see cref="Task"/> completes its delay, it checks if the local counts match the current counts.
        /// If either counter has changed during the delay (indicating a restart or cancellation), the timer takes no further action, effectively allowing a graceful restart or cancellation without needing to cancel the <see cref="Task"/> itself.
        /// </para>
        /// This approach avoids the overhead of task cancellation and ensures an orderly and predictable behavior for the timer.
        /// </summary>
        int _startCount = 0;
        int _cancelCount = 0;

        /// <summary>
        /// Starts or restarts the watchdog timer with optional actions for initial and completion phases.
        /// <para>
        /// This method encapsulates the common business logic shared by multiple overloads. It begins the timer 
        /// if it is not already running and allows for an initial action to be executed, either provided directly 
        /// or chosen from a default action if one is set. Upon expiration of the timer interval, a completion action
        /// is invoked unless canceled. The timer can be reset by calling this method again before the interval elapses. 
        /// The <see cref="RanToCompletion"/> event is always raised when the timer completes successfully.
        /// </para>
        /// </summary>
        /// <param name="initialAction">An optional action to execute when the timer starts. 
        /// If this parameter is null and <see cref="DefaultInitialAction"/> is not null, 
        /// the <see cref="DefaultInitialAction"/> will execute.</param>
        /// <param name="completeAction">An optional action to execute upon successful completion of the timer. 
        /// If this parameter is null and <see cref="DefaultCompleteAction"/> is not null, 
        /// the <see cref="DefaultCompleteAction"/> will execute.</param>
        /// <param name="e">Optional event arguments to pass to the completion event.</param>
        private void StartOrRestartInternal(
            Action initialAction = null,
            Action completeAction = null,
            EventArgs e = null)
        {
            if (!Running)
            {
                CompleteAction = null;  // Keep this a separate concern from virtual initialization.
                UserEventArgs = EventArgs.Empty;
                InitializeEpoch();
                OnEpochInitialized(new WatchdogTimerEventArgs(
                    initialAction: initialAction ?? DefaultInitialAction,
                    userEvent: e));
            }
            if(completeAction is not null)
            {
                CompleteAction = completeAction;
            }
            if(e is not null)
            {
                UserEventArgs = e;
            }

            // Increment start count for tracking
            var capturedStartCount = Interlocked.Increment(ref _startCount);
            var capturedCancelCount = Volatile.Read(ref _cancelCount);

            Task
                .Delay(Interval) // Delay for the specified interval
                .GetAwaiter()
                .OnCompleted(() =>
                {
                    // If the 'captured' localCount has not changed after awaiting the Interval, 
                    // it indicates that no new 'bones' have been thrown during that interval.        
                    if (capturedStartCount.Equals(Volatile.Read(ref _startCount)) && capturedCancelCount.Equals(_cancelCount))
                    {
                        OnRanToCompletion(new WatchdogTimerEventArgs(CompleteAction ?? DefaultCompleteAction, UserEventArgs));
                        // Fire and forget. Do not block a new epoch.
                        // Signal the current awaiting subscribers when it either returns or user sets the TCS result.
                        _ = OnCommitEpochAsync(new WatchdogTimerFinalizeEventArgs(EpochTaskCompletionSource), isCanceled: false);
                    }
                });
        }

        private void InitializeEpoch()
        {
            EpochTaskCompletionSource =
                new TaskCompletionSource<TaskStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        #region O V E R R I D E S 
        protected virtual void OnEpochInitialized(WatchdogTimerEventArgs e)
        {
            Running = true;
            e.Action?.Invoke(); // Execute initial action if set
            EpochInitialized?.Invoke(this, e);
        }

        protected virtual void OnRanToCompletion(WatchdogTimerEventArgs e)
        {
            Running = false; // Mark timer as no longer running
            RanToCompletion?.Invoke(this, e ?? EventArgs.Empty); // This is, of course, ALWAYS raised.
            e.Action?.Invoke(); // Execute the completion action
        }

        protected virtual void OnCanceled()
        {
            Running = false;
            Cancelled?.Invoke(this, EventArgs.Empty);
            // Fire and forget. Do not block a new epoch.
            // Signal the current awaiting subscribers when it either returns or user sets the TCS result.
            _ = OnCommitEpochAsync(new WatchdogTimerFinalizeEventArgs(EpochTaskCompletionSource), isCanceled: true);
        }
        #endregion O V E R R I D E S

        /// <summary>
        /// Finalize with user option to defer release.
        /// </summary>
        /// <remarks>
        /// - Cancellation is signaled based on the task result.
        /// - By contract, this object will never raise a TaskCanceledException.
        /// - [Careful]
        ///   e.TCS.TrySetException(...) will do that very thing, so don't do it.
        /// </remarks>
        protected virtual async Task OnCommitEpochAsync(WatchdogTimerFinalizeEventArgs e, bool isCanceled)
        {
            if (CommitEpochAsync is not null)
            {
                await CommitEpochAsync(isCanceled);
            }
            if (isCanceled)
            {
                e.TCS.TrySetResult(TaskStatus.Canceled);
            }
            else
            {
                e.TCS.TrySetResult(TaskStatus.RanToCompletion);
            }
        }
        /// <summary>
        /// Optional asynchronous callback invoked during epoch commitment, before the
        /// epoch completion result is published to awaiters.
        /// </summary>
        /// <remarks>
        /// This delegate provides a composition-based extension point for participating
        /// in the epoch lifecycle without subclassing <see cref="WatchdogTimer"/>.
        ///
        /// When assigned, the delegate is awaited during <c>OnCommitEpochAsync</c>, prior
        /// to resolving the epoch's completion task. This allows callers to perform
        /// asynchronous work that must complete while the epoch is still considered active.
        ///
        /// Behavioral contract:
        /// - The delegate is invoked at most once per epoch.
        /// - The delegate is invoked for both normal completion and cancellation.
        /// - The <paramref name="isCanceled"/> parameter indicates whether the epoch is
        ///   completing due to cancellation.
        /// - Exceptions thrown by the delegate are not converted to cancellation and
        ///   will propagate normally.
        /// - By contract, this mechanism will never raise <see cref="TaskCanceledException"/>.
        ///
        /// Implementations should avoid blocking operations and must not attempt to
        /// complete or fault the epoch task directly.
        /// </remarks>
        public OnCommitEpochDLGT CommitEpochAsync { get; set; }


        /// <summary>
        /// Restarts the watchdog timer using default completion actions.
        /// </summary>
        /// <remarks>
        /// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
        /// On completion, an event is fired with an empty <see cref="EventArgs"/> object.
        /// This overload does not specify an initial action, but if <see cref="DefaultInitialAction"/> is set, it will be executed. 
        /// This overload does not specify a completion action, but if <see cref="DefaultCompleteAction"/> is set, it will be executed. 
        /// </remarks>
        public void StartOrRestart() =>
            StartOrRestartInternal();

        /// <summary>
        /// Restarts the watchdog timer using default completion actions and specified event arguments.
        /// </summary>
        /// <remarks>
        /// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
        /// On completion, an event is fired with the provided <see cref="EventArgs"/> object.
        /// This overload does not specify an initial action, but if <see cref="DefaultInitialAction"/> is set, it will be executed. 
        /// This overload does not specify a completion action, but if <see cref="DefaultCompleteAction"/> is set, it will be executed. 
        /// </remarks>
        /// <param name="e">An optional <see cref="EventArgs"/> object to pass to the completion event. 
        /// If null, an empty <see cref="EventArgs"/> will be used.</param>
        public void StartOrRestart(EventArgs e) =>
            StartOrRestartInternal(e: e);

        /// <summary>
        /// Restarts the watchdog timer using a specified completion action.
        /// </summary>
        /// <remarks>
        /// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
        /// On completion, an event is fired with an empty <see cref="EventArgs"/> object.
        /// This overload does not specify an initial action, but if <see cref="DefaultInitialAction"/> is set, it will be executed. 
        /// The provided completion action will be executed upon successful completion of the timer, overriding the <see cref="DefaultCompleteAction"/>.
        /// </remarks>
        /// <param name="action">The action to execute upon successful completion of the timer. 
        /// This parameter cannot be null and will override the <see cref="DefaultCompleteAction"/> if it is set.</param>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> parameter is null.</exception>
        public void StartOrRestart(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action), "The action parameter cannot be null.");
            }
            StartOrRestartInternal(completeAction: action);
        }

        /// <summary>
        /// Restarts the watchdog timer using a specified completion action and event arguments.
        /// </summary>
        /// <remarks>
        /// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
        /// On completion, an event is fired with the provided <see cref="EventArgs"/> object.
        /// This overload does not specify an initial action, but if <see cref="DefaultInitialAction"/> is set, it will be executed. 
        /// The provided completion action will be executed upon successful completion of the timer, overriding the <see cref="DefaultCompleteAction"/>.
        /// </remarks>
        /// <param name="action">The action to execute upon successful completion of the timer. 
        /// This parameter cannot be null and will override the <see cref="DefaultCompleteAction"/> if it is set.</param>
        /// <param name="e">An optional <see cref="EventArgs"/> object to pass to the completion event. 
        /// If null, an empty <see cref="EventArgs"/> will be used.</param>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> parameter is null.</exception>
        public void StartOrRestart(Action action, EventArgs e)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action), "The action parameter cannot be null.");
            }
            StartOrRestartInternal(completeAction: action, e: e);
        }

        /// <summary>
        /// Restarts the watchdog timer using specified initial and completion actions.
        /// </summary>
        /// <remarks>
        /// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
        /// On completion, an event is fired with an empty <see cref="EventArgs"/> object.
        /// This overload allows clients to specify both an initial action and a completion action, 
        /// and both actions will override <see cref="DefaultInitialAction"/> and <see cref="DefaultCompleteAction"/> if they are set.
        /// </remarks>
        /// <param name="initialAction">The action to execute when starting the timer. 
        /// This parameter cannot be null and will override the <see cref="DefaultInitialAction"/> if it is set.</param>
        /// <param name="completeAction">The action to execute upon successful completion of the timer. 
        /// This parameter cannot be null and will override the <see cref="DefaultCompleteAction"/> if it is set.</param>
        /// <exception cref="ArgumentNullException">Thrown when either <paramref name="initialAction"/> or <paramref name="completeAction"/> is null.</exception>
        public void StartOrRestart(Action initialAction, Action completeAction)
        {
            if (initialAction == null)
            {
                throw new ArgumentNullException(nameof(initialAction), "The action parameter cannot be null.");
            }
            if (completeAction == null)
            {
                throw new ArgumentNullException(nameof(completeAction), "The action parameter cannot be null.");
            }
            StartOrRestartInternal(initialAction: initialAction, completeAction: completeAction);
        }

        /// <summary>
        /// Cancels the current timer, preventing any pending completion actions and events.
        /// </summary>
        public void Cancel()
        {
            Interlocked.Increment(ref _cancelCount);
            OnCanceled();
        }

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets a value indicating whether the timer is currently running.
        /// </summary>
        /// <value><c>true</c> if the timer is running; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// The running state is managed internally by the <see cref="WatchdogTimer"/> class and cannot be set externally.
        /// When the running state changes, the <see cref="PropertyChanged"/> event is triggered to notify any subscribers.
        /// </remarks>
        public bool Running
        {
            get => _running;
            protected set
            {
                if (!Equals(_running, value))
                {
                    _running = value;
                    OnPropertyChanged();
                }
            }
        }
        bool _running = default;

        /// <summary>
        /// Raised when an idle timer receives start via StartOrRestart and transitions to a Running state.
        /// </summary>
        public event EventHandler<WatchdogTimerEventArgs> EpochInitialized;

        /// <summary>
        /// Raised when an idle timer transitions to a Running state as the result of a StartOrRestart call,
        /// establishing a new epoch.
        /// </summary>
        public event EventHandler RanToCompletion;

        /// <summary>
        /// Raised when the timer is cancelled before completing its countdown.
        /// </summary>
        public event EventHandler Cancelled;

        #region A W A I T A B L E 
        /// <summary>
        /// Gets the completion source representing the current epoch.
        /// </summary>
        public TaskCompletionSource<TaskStatus> EpochTaskCompletionSource { get; private set; }

        public TaskAwaiter<TaskStatus> GetAwaiter() => EpochTaskCompletionSource.Task.GetAwaiter();
        #endregion A W A I T A B L E

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event to notify subscribers that a property value has changed.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed. This is optional and automatically provided by the compiler.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
