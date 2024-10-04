using System;
using System.ComponentModel;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace IVSoftware.Portable
{
    public class WatchdogTimer : INotifyPropertyChanged
    {
        public WatchdogTimer(Action initialAction = null, Action completeAction = null)
        {
            DefaultInitialAction = initialAction;
            DefaultCompleteAction = completeAction;
        }

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

                if (initialAction is Action alwaysAllow)
                {
                    alwaysAllow(); // Execute the provided initial action
                }
                else if (DefaultInitialAction is Action conditionalAllow)
                {
                    conditionalAllow(); // Execute default initial action if provided
                }
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
        /// This overload does not specify any initial action and disallows the use of <see cref="DefaultInitialAction"/> even if set, 
        /// relying solely on the <see cref="DefaultCompleteAction"/> if it is set.
        /// </remarks>
        public void StartOrRestart() =>
            StartOrRestartInternal();

        /// <summary>
        /// Restarts the watchdog timer using default completion actions and specified event arguments.
        /// </summary>
        /// <remarks>
        /// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
        /// On completion, an event is fired with the provided <see cref="EventArgs"/> object.
        /// This overload does not specify any initial action and disallows the use of <see cref="DefaultInitialAction"/> even if set, 
        /// relying solely on the <see cref="DefaultCompleteAction"/> if it is set.
        /// </remarks>
        /// <param name="e">An optional <see cref="EventArgs"/> object to pass to the completion event. 
        /// If null, an empty <see cref="EventArgs"/> will be used.</param>
        public void StartOrRestart(EventArgs e) =>
            StartOrRestartInternal(e: e);

        /// <summary>
        /// Restarts the watchdog timer using a specified completion action and event arguments.
        /// </summary>
        /// <remarks>
        /// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
        /// On completion, an event is fired with the provided <see cref="EventArgs"/> object.
        /// This overload does not specify any initial action and disallows the use of <see cref="DefaultInitialAction"/> even if set, 
        /// relying solely on the <see cref="DefaultCompleteAction"/> if it is set.
        /// </remarks>
        /// <param name="action">The action to execute upon successful completion of the timer. 
        /// This parameter cannot be null and may throw an <see cref="ArgumentNullException"/> if null.</param>
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
        /// Restarts the watchdog timer using a specified completion action.
        /// </summary>
        /// <remarks>
        /// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
        /// On completion, an event is fired with an empty <see cref="EventArgs"/> object.
        /// This overload does not specify any initial action and disallows the use of <see cref="DefaultInitialAction"/> even if set, 
        /// relying solely on the <see cref="DefaultCompleteAction"/> if it is set.
        /// </remarks>
        /// <param name="action">The action to execute upon successful completion of the timer. 
        /// This parameter cannot be null and may throw an <see cref="ArgumentNullException"/> if null.</param>
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
        /// Restarts the watchdog timer using specified initial and completion actions.
        /// </summary>
        /// <remarks>
        /// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
        /// On completion, an event is fired with an empty <see cref="EventArgs"/> object.
        /// This overload allows clients to specify both an initial action and a completion action.
        /// </remarks>
        /// <param name="initialAction">The action to execute when starting the timer. 
        /// This parameter cannot be null and may throw an <see cref="ArgumentNullException"/> if null.</param>
        /// <param name="completeAction">The action to execute upon successful completion of the timer. 
        /// This parameter cannot be null and may throw an <see cref="ArgumentNullException"/> if null.</param>
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

        public event EventHandler RanToCompletion;
        public event EventHandler Cancelled;

        private Action DefaultInitialAction { get; }
        private Action DefaultCompleteAction { get; }

        protected virtual void OnPropertyChanged()
        {
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
