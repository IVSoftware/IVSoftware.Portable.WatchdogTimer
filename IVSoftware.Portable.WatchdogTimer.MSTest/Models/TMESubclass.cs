using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IVSoftware.Portable.MSTest.Models
{
    /// <summary>
    /// Base class - the bridge.
    /// </summary>
    class TextBoxAwaitableBaseClass
        : TextBox           // A platform UI that we do not own...
        , IAwaitableEpoch   // Exposes epoch semantics through composition

    {
        private readonly IAwaitableEpoch _wdt = new WatchdogTimer();

        public TimeSpan Interval { get => _wdt.Interval; set => _wdt.Interval = value; }

        public bool Running => _wdt.Running;

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

        public void Cancel()
        {
            _wdt.Cancel();
        }

        public void StartOrRestart()
        {
            _wdt.StartOrRestart();
        }

        public void StartOrRestart(Action action)
        {
            _wdt.StartOrRestart(action);
        }

        public void StartOrRestart(Action initialAction, Action completeAction)
        {
            _wdt.StartOrRestart(initialAction, completeAction);
        }

        public void StartOrRestart(Action action, EventArgs e)
        {
            _wdt.StartOrRestart(action, e);
        }

        public void StartOrRestart(EventArgs e)
        {
            _wdt.StartOrRestart(e);
        }

        public event EventHandler EpochInitialized
        { 
            add => _wdt.EpochInitialized += value;
            remove => _wdt.EpochInitialized -= value;
        }

        public event EventHandler Cancelled
        {
            add
            {
                _wdt.Cancelled += value;
            }

            remove
            {
                _wdt.Cancelled -= value;
            }
        }

        public event EventHandler<EpochFinalizingAsyncEventArgs> EpochFinalizing
        {
            add
            {
                _wdt.EpochFinalizing += value;
            }

            remove
            {
                _wdt.EpochFinalizing -= value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                _wdt.PropertyChanged += value;
            }

            remove
            {
                _wdt.PropertyChanged -= value;
            }
        }

        public event EventHandler RanToCompletion
        {
            add
            {
                _wdt.RanToCompletion += value;
            }

            remove
            {
                _wdt.RanToCompletion -= value;
            }
        }
    }

    /// <summary>
    /// Subclass - blissfully unaware.
    /// </summary>
    class TextBoxAwaitable
        : TextBoxAwaitableBaseClass
    {
        public TimeSpan AsyncFinalizationPeriod {  get; set; } = TimeSpan.FromSeconds(1);
        protected override async Task OnEpochFinalizingAsync(EpochFinalizingAsyncEventArgs e)
        {
            await Task.Delay(AsyncFinalizationPeriod);
            await base.OnEpochFinalizingAsync(e);
        }
    }
}
