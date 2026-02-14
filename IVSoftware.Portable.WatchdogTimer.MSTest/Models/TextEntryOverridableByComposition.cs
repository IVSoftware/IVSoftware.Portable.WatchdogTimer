using IVSoftware.Portable.MSTest.Preview;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace IVSoftware.Portable.MSTest.Models
{
    class TextEntryOverridableByComposition
        : TextBox
        , IDisposable  // Encapsulates a disposable SQLiteAsyncConnection for test.
    {
        public TextEntryOverridableByComposition()
        {
            _wdt.EpochFinalizing += (sender, e) =>
            {
                e.QueueEpochTask(() => OnEpochFinalizingAsync(e));
            };
            _dhost.GetToken();
        }
        protected virtual async Task OnEpochFinalizingAsync(
            EpochFinalizingAsyncEventArgs e)
        {
            // await SomeAsyncWork(); // "Calls are taken in the order that they are received."
        }

        private readonly DHostSQLiteAsyncConnection _dhost = new(async (acnx) =>
        {
            await acnx.CreateTableAsync<Item>();
            await acnx.InsertAsync(new Item { Description = "Hello" });
        });

        private readonly WatchdogTimer _wdt = new WatchdogTimer { Interval = TimeSpan.FromSeconds(0.25) };
        public TaskAwaiter<TaskStatus> GetAwaiter() => _wdt.GetAwaiter();

        public ObservableCollection<Item> Items { get; } = new();

        public string InputText
        {
            get => base.Text;
            set
            {
                if (!Equals(base.Text, value))
                {
                    base.Text = value;
                    _wdt.StartOrRestart();
                }
            }
        }
        public void Dispose() => _dhost.Tokens.Single().Dispose();
    }
}
