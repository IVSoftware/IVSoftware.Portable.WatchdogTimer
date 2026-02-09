using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IVSoftware.Portable
{
    public class WatchdogTimerEventArgs : EventArgs
    {
        public WatchdogTimerEventArgs(Action initialAction, EventArgs userEvent)
        {
            Action = initialAction;
            UserEvent = userEvent;
        }
        public Action Action { get; }
        public EventArgs UserEvent { get; }
    }
    public class WatchdogTimerFinalizeEventArgs : EventArgs
    {
        public WatchdogTimerFinalizeEventArgs(TaskCompletionSource<TaskStatus> tcs)
        {
            TCS = tcs;
        }
        public TaskCompletionSource<TaskStatus> TCS { get; }
    }
}
