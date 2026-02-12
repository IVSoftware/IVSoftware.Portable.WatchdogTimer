using IVSoftware.Portable.MSTest.Preview;
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IVSoftware.Portable.MSTest.Models
{

    class TextEntryModelByComposition : IDisposable
    {
        public TextEntryModelByComposition()
        {
            _wdt.EpochFinalizing +=  (sender, e) => CommitEpoch(e);
            _dhost.GetToken();
        }

        private readonly DHostSQLiteAsyncConnection _dhost = new(async (acnx) =>
        {
            await acnx.CreateTableAsync<Item>();
            await acnx.InsertAsync(new Item { Description = "Hello" });
        });

        WatchdogTimer _wdt = new WatchdogTimer { Interval = TimeSpan.FromSeconds(0.25) };
        public TaskAwaiter<TaskStatus> GetAwaiter() => _wdt.GetAwaiter();

        public ObservableCollection<Item> Items { get; } = new();

        public string InputText
        {
            get => _inputText;
            set
            {
                if (!Equals(_inputText, value))
                {
                    _inputText = value;
                    _wdt.StartOrRestart();
                }
            }
        }
        string _inputText = string.Empty;

        private void CommitEpoch(EpochFinalizingAsyncEventArgs e)
        {
            if (!(e.IsCanceled || string.IsNullOrWhiteSpace(InputText)))
            {
                // Fire and forget here, but legitimately awaited in the event class.
                e.QueueEpochTask(async () =>
                { 
                    var acnx = await _dhost.GetCnx();
                    var recordset = await acnx.QueryAsync<Item>(
                        "SELECT * FROM Item WHERE Description LIKE ?",
                        $"%{InputText}%");
                    Items.Clear();
                    foreach (var item in recordset)
                    {
                        Items.Add(item);
                    }
                });
            }
        }
        public void Dispose() => _dhost.Tokens.Single().Dispose();
    }
}
