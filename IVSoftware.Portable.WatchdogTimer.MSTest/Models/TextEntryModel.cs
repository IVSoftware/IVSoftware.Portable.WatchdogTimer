using IVSoftware.Portable.MSTest.Preview;
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IVSoftware.Portable.MSTest.Models
{

    class TextEntryModel : WatchdogTimer, IDisposable
    {
        public TextEntryModel()
        {
            Interval = TimeSpan.FromSeconds(0.25);
            _dhost.GetToken();
        }

        private readonly DHostSQLiteAsyncConnection _dhost = new(async (acnx) =>
        {
            await acnx.CreateTableAsync<Item>();
            await acnx.InsertAsync(new Item { Description = "Hello" });
        });

        public ObservableCollection<Item> Items { get; } = new();

        public string InputText
        {
            get => _inputText;
            set
            {
                if (!Equals(_inputText, value))
                {
                    _inputText = value;
                    StartOrRestart();
                }
            }
        }
        string _inputText = string.Empty;

        protected override async Task OnEpochFinalizingAsync(EpochFinalizingAsyncEventArgs e)
        {
            if (!(e.IsCanceled || string.IsNullOrWhiteSpace(InputText)))
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
            }
            await base.OnEpochFinalizingAsync(e);
        }

        public void Dispose()=>_dhost.Tokens.Single().Dispose();
    }
}
