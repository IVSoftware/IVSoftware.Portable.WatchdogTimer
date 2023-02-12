using System;
using System.Threading.Tasks;

namespace IVSoftware.Portable
{
    public class WatchdogTimer
    {
        int _wdtCount = 0;
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);
        public void StartOrRestart(Action action = null)
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
                        RanToCompletion?.Invoke(this, EventArgs.Empty);
                    }
                });
        }
        public bool Running { get; private set; }
        public event EventHandler RanToCompletion;
    }
}
