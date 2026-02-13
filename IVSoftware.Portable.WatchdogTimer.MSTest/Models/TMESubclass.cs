using System.Runtime.CompilerServices;

namespace IVSoftware.Portable.MSTest.Models
{
    /// <summary>
    /// Base class - the bridge.
    /// </summary>
    class TextBoxAwaitableBaseClass
        : TextBox           // A platform UI that we do not own...
        , IAwaitableEpoch   // Basically, acts as though it inherited WatchdogTimer
    {
        private readonly WatchdogTimer _wdt = new();

        public TextBoxAwaitableBaseClass()
        {
            _wdt.EpochFinalizing += (sender, e) =>
                e.QueueEpochTask(() => OnEpochFinalizingAsync(e));
        }

        // Represents an ordered async workload participating in settlement.
        protected virtual Task OnEpochFinalizingAsync(EpochFinalizingAsyncEventArgs e) => Task.CompletedTask;

        protected override void OnTextChanged()
        {
            base.OnTextChanged();
            _wdt.StartOrRestart();
        }
        public TaskAwaiter<TaskStatus> GetAwaiter() => _wdt.GetAwaiter();

        public event EventHandler EpochInitialized
        { 
            add => _wdt.EpochInitialized += value;
            remove => _wdt.EpochInitialized -= value;
        }
    }

    /// <summary>
    /// Subclass - blissfully unaware.
    /// </summary>
    class TextBoxAwaitable
        : TextBoxAwaitableBaseClass
    {
        protected override async Task OnEpochFinalizingAsync(EpochFinalizingAsyncEventArgs e)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await base.OnEpochFinalizingAsync(e);
        }
    }
}
