using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace IVSoftware.Portable
{
    public interface IAwaitableEpoch
    {
        TimeSpan Interval { get; set; }
        bool Running { get; }

        event EventHandler Cancelled;
        event EventHandler<EpochFinalizingAsyncEventArgs> EpochFinalizing;
        event EventHandler EpochInitialized;
        event PropertyChangedEventHandler PropertyChanged;
        event EventHandler RanToCompletion;

        void Cancel();
        void StartOrRestart();
        void StartOrRestart(Action action);
        void StartOrRestart(Action initialAction, Action completeAction);
        void StartOrRestart(Action action, EventArgs e);
        void StartOrRestart(EventArgs e);

        /// <summary>
        /// Explicitly exposes awaitability for implementers referenced via the interface.
        /// </summary>
        TaskAwaiter<TaskStatus> GetAwaiter();
    }
}
