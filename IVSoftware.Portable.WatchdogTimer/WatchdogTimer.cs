using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

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
        }
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
            if (e is null) e = EventArgs.Empty; // Ensure event args is not null
            if (!Running)
            {
                Running = true;
                (initialAction ?? DefaultInitialAction)?.Invoke(); // Execute initial action if set
            }

            _startCount++; // Increment start count for tracking
            var capturedStartCount = _startCount;
            var capturedCancelCount = _cancelCount;

            Task
                .Delay(Interval) // Delay for the specified interval
                .GetAwaiter()
                .OnCompleted(() =>
                {
                    // If the 'captured' localCount has not changed after awaiting the Interval, 
                    // it indicates that no new 'bones' have been thrown during that interval.        
                    if (capturedStartCount.Equals(_startCount) && capturedCancelCount.Equals(_cancelCount))
                    {
                        Running = false; // Mark timer as no longer running
                        RanToCompletion?.Invoke(this, e ?? EventArgs.Empty); // This is, of course, ALWAYS raised.
                        (completeAction ?? DefaultCompleteAction)?.Invoke(); // Execute the completion action
                    }
                });
        }

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
        /// Cancel all pending actions and events.
        /// </summary>
        public void Cancel()
        {
            _cancelCount++;
            Running = false;
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

        public bool Running
        {
            get => _running;
            set
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
        /// Raised when the timer successfully completes its countdown and the completion action is invoked.
        /// </summary>
        public event EventHandler RanToCompletion;

        /// <summary>
        /// summary when the timer is cancelled before completing its countdown.
        /// </summary>
        public event EventHandler Cancelled;

        /// <summary>
        /// Gets the default action to be executed when the timer starts, if no other initial action is provided.
        /// </summary>
        private Action DefaultInitialAction { get; }

        /// <summary>
        /// Gets the default action to be executed upon successful completion of the timer, if no other completion action is provided.
        /// </summary>
        private Action DefaultCompleteAction { get; }

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
