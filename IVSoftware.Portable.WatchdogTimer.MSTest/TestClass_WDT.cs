using IVSoftware.Portable.Disposable;
using IVSoftware.Portable.MSTest.Models;
using IVSoftware.Portable.MSTest.Preview;
using IVSoftware.Portable.Xml.Linq.XBoundObject.Modeling;
using IVSoftware.WinOS.MSTest.Extensions;
using Ignore = Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute;
using Newtonsoft.Json;
using SQLite;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace IVSoftware.Portable.MSTest
{
    [TestClass, DoNotParallelize]
    public sealed class TestClass_WDT
    {
        [TestMethod]
        public async Task Test_WDT()
        {
            var stopwatch = new Stopwatch(); 
            double expectedTimeout;
            double expectedElapsed;
            await subtestSingletonWithInitialAndComplete();
            
            async Task subtestSingletonWithInitialAndComplete()
            {
                var uut = new SingletonWithInitialAndComplete();
                expectedTimeout = uut.WDT.Interval.TotalSeconds;
                expectedElapsed = 10 * 0.25 + expectedTimeout;

                Assert.IsFalse(uut.WDT.Running);

                stopwatch.Restart();
                uut.WDT.StartOrRestart();

                Assert.AreEqual(
                    WDTEventId.Initial,
                    uut.EventQueue.DequeueSingleWDTTestEvent().WDTEventId,
                    "Expecting WDT to raise Initial event id.");

                uut.WDT.ExecStartOrRestartLoop();
                await uut;
                stopwatch.Stop();

                Assert.IsFalse(uut.WDT.Running);

                Assert.AreEqual(
                    WDTEventId.Complete,
                    uut.EventQueue.DequeueSingleWDTTestEvent().WDTEventId,
                    "Expecting WDT to raise Complete event id.");

                var elapsed = stopwatch.Elapsed.TotalSeconds;

                // [Careful]
                // Works only if you haven't paused the
                // test (like with debug breakpoints).
                Assert.IsTrue(
                    elapsed > expectedElapsed - 0.5 && elapsed < expectedElapsed + 0.5,
                    $"Elapsed time out of expected range: {elapsed:N2} sec");

                Assert.AreEqual(2, uut.Toggle);

                uut.WDT.StartOrRestart(() =>
                {
                    Assert.Fail("Expecting this completion does not occur.");
                });

                Assert.AreEqual(
                    WDTEventId.Initial,
                    uut.EventQueue.DequeueSingleWDTTestEvent().WDTEventId,
                    "Expecting WDT to raise Initial event id.");

                uut.WDT.Cancel();
                await uut;
                Assert.AreEqual(
                    0,
                    uut.EventQueue.Count);
                Assert.IsFalse(uut.WDT.Running);
            }
        }

        [TestMethod]
        public async Task Test_Awaitable()
        {
            WatchdogTimer wdt = new ();
            Stopwatch stopwatch = new ();
            TaskCompletionSource tcsInit = new (), tcsRTC = new ();
            wdt.EpochInitialized += (sender, e) =>
            {
                tcsInit.SetResult();
            };
            wdt.RanToCompletion += (sender, e) =>
            {
                tcsRTC.SetResult();
            };
            await wdt;
            { }
            stopwatch.Restart();
            wdt.StartOrRestart();
            await wdt;
            stopwatch.Stop();
            Assert.IsTrue(stopwatch.Elapsed > TimeSpan.FromSeconds(0.9));

            await tcsInit.Task;
            await tcsRTC.Task;
        }


        [TestMethod]
        public async Task Test_CanceledAwaitable()
        {
            WatchdogTimer wdt = new();
            Stopwatch stopwatch = new();
            TaskCompletionSource
                tcsInit = new(), 
                tcsCanceled = new(), 
                tcsRTC = new();
            wdt.EpochInitialized += (sender, e) =>
            {
                tcsInit.TrySetResult();
            };
            wdt.RanToCompletion += (sender, e) =>
            {
                tcsRTC.TrySetResult();
            };
            wdt.Cancelled += (sender, e) =>
            {
                tcsCanceled.TrySetResult();
            };
            await wdt;

            stopwatch.Restart();
            wdt.StartOrRestart();
            Task
                .Delay(TimeSpan.FromSeconds(0.5))
                .GetAwaiter()
                .OnCompleted(() => wdt.Cancel());
            var taskStatus = await wdt;
            stopwatch.Stop();
            Assert.AreEqual(TaskStatus.Canceled, taskStatus);
            Assert.IsTrue(stopwatch.Elapsed > TimeSpan.FromSeconds(0.4));
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(0.6));

            await tcsInit.Task;
            await tcsCanceled.Task;
        }

        [TestMethod]
        public async Task Test_Subclass()
        {
            WatchdogTimerWithExtendedCommit wdt = new();
            Stopwatch stopwatch = new();
            TaskCompletionSource tcsInit = new(), tcsRTC = new();
            wdt.EpochInitialized += (sender, e) =>
            {
                tcsInit.SetResult();
            };
            wdt.RanToCompletion += (sender, e) =>
            {
                tcsRTC.SetResult();
            };
            await wdt;
            { }
            stopwatch.Restart();
            wdt.StartOrRestart();
            await wdt;
            stopwatch.Stop();

            Assert.IsTrue(
                stopwatch.Elapsed > TimeSpan.FromSeconds(1.9),
                "Expecting the commit async operation to extend the default WDT interval.");

            await tcsInit.Task;
            await tcsRTC.Task;
        }

        /// <summary>
        /// Demonstrates simulated async work within the WDT epoch.
        /// </summary>
        /// <remarks>
        /// If caller is awaiting the WDT than they're awaiting this work as well.
        /// </remarks>
        class WatchdogTimerWithExtendedCommit : WatchdogTimer
        {
            protected override async Task OnEpochFinalizingAsync(EpochFinalizingAsyncEventArgs e)
            {
                // Simulate work - like an async query.
                await Task.Delay(TimeSpan.FromSeconds(1));

                await base.OnEpochFinalizingAsync(e);
            }
        }

        [TestMethod]
        public async Task Test_TextEntryModel()
        {
            string actual, expected;
            using TextEntryModel entry = new();
            entry.InputText = "hello";
            await entry;

            actual = JsonConvert.SerializeObject(entry.Items);
            expected = @"[{""Id"":1,""Description"":""Hello""}]";
            Assert.AreEqual(expected.NormalizeResult(), actual.NormalizeResult(), "Expecting json to match." );
        }

        [TestMethod]
        public async Task Test_TextEntryModelByComposition()
        {
            string actual, expected;
            using TextEntryModelByComposition entry = new();
            entry.InputText = "hello";
            await entry;

            actual = JsonConvert.SerializeObject(entry.Items);
            expected = @"[{""Id"":1,""Description"":""Hello""}]";
            Assert.AreEqual(expected.NormalizeResult(), actual.NormalizeResult(), "Expecting json to match." );
        }

        /// <summary>
        /// Legacy test class from before WDT was awaitable.
        /// </summary>
        class SingletonWithInitialAndComplete
        {
            public Queue<SenderEventPair> EventQueue { get; } = new();
            public Queue<SenderEventPair> PropertyChangedQueue { get; } = new();

            public int Toggle { get; private set; } = 0;

            public WatchdogTimer WDT
            {
                get
                {
                    if (_wdt is null)
                    {
                        _wdt = new WatchdogTimer(
                            defaultInitialAction: () =>
                            {
                                _tcs = new TaskCompletionSource();
                                EventQueue.Enqueue((_wdt, new WDTTestEventArgs(WDTEventId.Initial)));
                            },
                            defaultCompleteAction: () =>
                            {
                                EventQueue.Enqueue((_wdt, new WDTTestEventArgs(WDTEventId.Complete)));
                                _tcs.TrySetResult();
                            });
                        _wdt.PropertyChanged += (sender, e) =>
                        {
                            PropertyChangedQueue.Enqueue((sender, e));
                            switch (e.PropertyName)
                            {
                                case nameof(WDT.Running):
                                    Toggle++;
                                    switch (Toggle % 2)
                                    {
                                        case 1:
                                            Assert.IsTrue(WDT.Running);
                                            break;
                                        case 0:
                                            Assert.IsFalse(WDT.Running);
                                            break;
                                    }
                                    break;
                            }
                        };
                        _wdt.Cancelled += (sender, e) =>
                        {
                            _tcs.TrySetResult();
                        };
                    }
                    return _wdt;
                }
            }
            private WatchdogTimer? _wdt = null;
            private TaskCompletionSource _tcs = new();
            public TaskAwaiter GetAwaiter() => _tcs.Task.GetAwaiter();
        }

        [TestMethod]
        public async Task Test_AsyncSQLiteConnection()
        {
            string actual, expected;

            DHostSQLiteAsyncConnection dhost = new (async(acnx) =>
            {
                await acnx.CreateTableAsync<Item>();
                await acnx.InsertAsync(new Item { Description = "Hello" });
            });

            using (dhost.GetToken())
            {
                var acnx = await dhost.GetCnx();
                var recordset = await acnx.Table<Item>().ToListAsync();
                actual = JsonConvert.SerializeObject(recordset);
                expected = @"[{""Id"":1,""Description"":""Hello""}]";
                Assert.AreEqual(expected.NormalizeResult(), actual.NormalizeResult(), "Expecting json to match.");
            }
            await dhost;

            using (dhost.GetToken())
            {
                var acnx = await dhost.GetCnx();
                var recordset = await acnx.Table<Item>().ToListAsync();
                actual = JsonConvert.SerializeObject(recordset);
                expected = @"[{""Id"":1,""Description"":""Hello""}]";
                Assert.AreEqual(expected.NormalizeResult(), actual.NormalizeResult(), "Expecting json to match.");
            }
        }
    }
    class WDTTestEventArgs : EventArgs
    {
        public WDTTestEventArgs(WDTEventId wdtEventId)
        {
            WDTEventId = wdtEventId;
        }

        public WDTEventId WDTEventId { get; }
    }
    enum WDTEventId
    {
        Initial,
        Complete,
    }
    static class MSTestExtensions
    {
        public static void ExecStartOrRestartLoop(this WatchdogTimer @this, int loopN = 10, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.FromSeconds(0.25);
            _ = localStartOrRestart();
            async Task localStartOrRestart()
            {
                for (int i = 0; i < loopN; i++)
                {
                    await Task.Delay((TimeSpan)delay);
                    Assert.IsTrue(@this.Running);
                    @this.StartOrRestart();
                }
            }
        }
        public static T DequeueSingle<T>(this Queue<T> queue)
        {
            switch (queue.Count)
            {
                case 0:
                    throw new InvalidOperationException("Queue is empty.");
                case 1:
                    return queue.Dequeue();
                default:
                    throw new InvalidOperationException("Multiple items in queue.");
            }
        }
        public static WDTTestEventArgs DequeueSingleWDTTestEvent(this Queue<SenderEventPair> queue)
        {
            switch (queue.Count)
            {
                case 0:
                    throw new InvalidOperationException("Queue is empty.");
                case 1:
                    return (WDTTestEventArgs)queue.Dequeue().e;
                default:
                    throw new InvalidOperationException("Multiple items in queue.");
            }
        }
    }
}
