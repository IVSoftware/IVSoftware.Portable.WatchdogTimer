using System;
using System.Threading.Tasks;

namespace IVSoftware.Portable
{
    public class WatchdogTimer
    {
        int _wdtCount = 0;
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);
        public void StartOrRestart(Action action, EventArgs e)
        {
            Running = true;
            _wdtCount++;
            var capturedCount = _wdtCount;
            Task
                .Delay(Interval)
                .GetAwaiter()
                .OnCompleted(() =>
                {
                    // If the 'captured' localCount has not changed after awaiting the Interval, 
                    // it indicates that no new 'bones' have been thrown during that interval.        
                    if (capturedCount.Equals(_wdtCount))
                    {
                        action?.Invoke();
                        Running = false;
                        RanToCompletion?.Invoke(this, e ?? EventArgs.Empty);
                    }
                });
        }

        public void StartOrRestart() => StartOrRestart(null, EventArgs.Empty);
        public void StartOrRestart(Action action) => StartOrRestart(action, EventArgs.Empty);
        public void StartOrRestart(EventArgs e) => StartOrRestart(null, e);
        
        // By decrementing without capturing, the condition
        // capturedCount.Equals(_wdtCount) evaluated to false.
        public void Cancel() => _wdtCount = -1;
        public bool Running { get; private set; }
        public event EventHandler RanToCompletion;
    }
}
