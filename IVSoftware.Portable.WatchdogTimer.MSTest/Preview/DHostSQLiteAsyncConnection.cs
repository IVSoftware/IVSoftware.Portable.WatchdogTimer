using IVSoftware.Portable.Disposable;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IVSoftware.Portable.MSTest.Preview
{

    class DHostSQLiteAsyncConnection : AwaitableDisposableHost
    {
        public DHostSQLiteAsyncConnection(Func<SQLiteAsyncConnection, Task>? init = null)
        {
            _init = init;
        }
        SQLiteAsyncConnection _acnx = null!;

        private readonly Func<SQLiteAsyncConnection, Task>? _init;
        protected override async Task OnFinalDisposeAsync(FinalDisposeAsyncEventArgs e)
        {
            if (_acnx != null)
            {
                await _acnx.CloseAsync();
                _acnx = null!;
            }
            await base.OnFinalDisposeAsync(e);
        }
        protected override async void OnFinalDispose(FinalDisposeEventArgs e)
        {
            base.OnFinalDispose(e);
        }

        public async Task<SQLiteAsyncConnection> GetCnx()
        {
            if (IsZero())
            {
                throw new InvalidOperationException(
                    "GetCnx must be called within an active DisposableHost token scope.");
            }
            else
            {
                if (_acnx is null)
                {
                    using (GetToken())
                    {
                        _acnx = new SQLiteAsyncConnection(":memory:");
                        if (_init is not null)
                        {
                            await _init(_acnx);
                        }
                        return _acnx;
                    }
                }
                else
                {
                    return _acnx;
                }
            }
        }
    }
}
