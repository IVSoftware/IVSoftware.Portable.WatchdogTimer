using System;
using System.Threading.Tasks;

namespace IVSoftware.Portable
{
    public class WatchdogTimer
    {
        int _startCount = 0;
        int _cancelCount = 0;

        /// <summary>
        /// Restart the watchdog timer.
        /// </summary>
        /// <remarks>
        /// Core method that can take a parameterized action as well as a custom EventArgs object.
        /// </remarks>
        public void StartOrRestart(Action action, EventArgs e)
        {
            Running = true;
            _startCount++;
            var capturedStartCount = _startCount;
            var capturedCancelCount = _cancelCount;
            Task
                .Delay(Interval)
                .GetAwaiter()
                .OnCompleted(() =>
                {
                    // If the 'captured' localCount has not changed after awaiting the Interval, 
                    // it indicates that no new 'bones' have been thrown during that interval.        
                    if (capturedStartCount.Equals(_startCount) && capturedCancelCount.Equals(_cancelCount))
                    {
                        Running = false;
                        RanToCompletion?.Invoke(this, e ?? EventArgs.Empty);
                        action?.Invoke();
                    }
                });
        }

        /// <summary>
        /// Restart the watchdog timer.
        /// </summary>
        /// <remarks>
        /// Subscribe to the RanToCompletion event to receive notification of completion.  
        /// On completion, fire an event with an empty EventArgs object.
        /// </remarks>
        public void StartOrRestart() => StartOrRestart(null, EventArgs.Empty);

        /// <summary>
        /// Restart the watchdog timer.
        /// </summary>
        /// <remarks>
        /// Subscribe to the RanToCompletion event to receive notification of completion.  
        /// On completion, fire an event using a custom parameterized EventArgs object.
        /// </remarks>
        public void StartOrRestart(EventArgs e) => StartOrRestart(null, e);

        /// <summary>
        /// Restart the watchdog timer.
        /// </summary>
        /// <remarks>
        /// Subscribe to the RanToCompletion event to receive notification of completion.  
        /// On completion, invoke a parameterized action.
        /// </remarks>
        public void StartOrRestart(Action action) => StartOrRestart(action, EventArgs.Empty);

        /// <summary>
        /// Restart the watchdog timer.
        /// </summary>
        /// <remarks>
        /// Invoke an initial parameterized action if not already running.
        /// Subscribe to the RanToCompletion event to receive notification of completion.  
        /// On completion, invoke a parameterized action.
        /// </remarks>
        public void StartOrRestart(Action initialAction, Action completeAction)
        {
            if (!Running)
            {
                Running = true;
                initialAction();
            }
            StartOrRestart(completeAction, EventArgs.Empty);
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
        public bool Running { get; private set; }
        public event EventHandler RanToCompletion;
        public event EventHandler Cancelled;
    }
}
