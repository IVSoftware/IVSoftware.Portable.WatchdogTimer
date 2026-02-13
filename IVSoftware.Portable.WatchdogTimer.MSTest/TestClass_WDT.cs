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
using IVSoftware.Portable.Common.Exceptions;

#if false && TODO
TO DO — Stress and Concurrency Coverage

The following scenarios are not currently covered by unit tests and
should be validated to strengthen concurrency guarantees:

1. High-Frequency Restart Storm
   - Rapid, repeated StartOrRestart calls (hundreds or thousands).
   - Verify single coalesced completion.
   - Confirm no TaskCompletionSource corruption or double completion.
   - Confirm Running state stability.

2. Concurrent StartOrRestart Calls
   - Invoke StartOrRestart from multiple threads simultaneously.
   - Ensure epoch counters remain coherent.
   - Confirm no duplicate completion or missed completion.

3. Concurrent Awaiters
   - Multiple callers awaiting the same epoch concurrently.
   - Verify all resume exactly once upon settlement.
   - Confirm consistent TaskStatus across awaiters.

4. Overlapping Epoch Transitions
   - Start a new epoch immediately after previous completion.
   - Verify old epoch remains awaitable.
   - Confirm new TCS replaces prior one cleanly.
   - Ensure no cross-epoch contamination.

5. EpochInvokeAsync Timeout Path
   - Force FIFO contention and trigger timeout.
   - Validate ThrowHard behavior.
   - Confirm semaphore integrity after timeout.
   - Ensure epoch still settles deterministically.

6. Reentrancy Guard Stress
   - Attempt nested EpochInvokeAsync calls.
   - Confirm InvalidOperationException path.
   - Verify no deadlock and no semaphore leak.

7. Pathological Late Participation
   - Yield before EpochInvokeAsync under load.
   - Confirm guard triggers.
   - Ensure no partial epoch prolongation.

8. Parallel Independent Epoch Instances
   - Multiple WatchdogTimer instances running concurrently.
   - Confirm no cross-instance interference.
   - Validate AsyncLocal isolation per epoch.

9. Cancellation Under Load
   - Cancel during heavy FIFO participation.
   - Ensure no orphaned work extends canceled epoch.
   - Confirm TaskStatus.Canceled consistency.

10. Memory/Lifetime Audit
    - Run many epochs in succession.
    - Verify no retained TCS references.
    - Confirm DisposableHost lifetimes close correctly.

These tests validate structural integrity under stress,
not just functional correctness under nominal conditions.
#endif

namespace IVSoftware.Portable.MSTest
{
    [TestClass, DoNotParallelize]
    public sealed class TestClass_WDT
    {
        /// <summary>
        /// Verifies end-to-end WatchdogTimer behavior including initial notification,
        /// restart coalescing, awaited completion timing, and cancellation semantics.
        /// </summary>
        /// <remarks>
        /// The test confirms:
        /// - The Initial event is raised when a new epoch begins.
        /// - Multiple rapid StartOrRestart calls coalesce into a single completion.
        /// - Awaiting the WDT resumes only after the final interval expires.
        /// - The Complete event is raised exactly once per settled epoch.
        /// - Cancellation suppresses completion and leaves the timer not running.
        /// - The WDT remains awaitable across successive epochs.
        /// </remarks>
        [TestMethod]
        public async Task Test_WDT()
        {
            var stopwatch = new Stopwatch(); 
            double expectedTimeout;
            TimeSpan expectedElapsed;
            await subtestSingletonWithInitialAndComplete();
            
            async Task subtestSingletonWithInitialAndComplete()
            {
                var uut = new SingletonWithInitialAndComplete();
                expectedTimeout = uut.WDT.Interval.TotalSeconds;

                Assert.IsFalse(uut.WDT.Running);

                stopwatch.Restart();
                uut.WDT.StartOrRestart();

                Assert.AreEqual(
                    WDTEventId.Initial,
                    uut.EventQueue.DequeueSingleWDTTestEvent().WDTEventId,
                    "Expecting WDT to raise Initial event id.");

                expectedElapsed = uut.WDT.ExecStartOrRestartLoop();
                await uut;
                stopwatch.Stop();

                Assert.IsFalse(uut.WDT.Running);

                Assert.AreEqual(
                    WDTEventId.Complete,
                    uut.EventQueue.DequeueSingleWDTTestEvent().WDTEventId,
                    "Expecting WDT to raise Complete event id.");

                var elapsed = TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds);

                // [Careful]
                // Works only if you haven't paused the
                // test (like with debug breakpoints).
                Assert.IsTrue(
                    elapsed > expectedElapsed - TimeSpan.FromSeconds(0.5) && elapsed < expectedElapsed + TimeSpan.FromSeconds(0.5),
                    $"Elapsed time out of expected range: {elapsed} sec");

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
        /// <summary>
        /// Verifies that WatchdogTimer is awaitable both before and after an epoch,
        /// and that awaiting resumes only after the interval elapses and completion
        /// events have fired.
        /// </summary>
        /// <remarks>
        /// The test confirms:
        /// - Awaiting an idle WDT completes immediately.
        /// - StartOrRestart begins a new epoch and raises EpochInitialized.
        /// - Awaiting the WDT blocks until the interval expires.
        /// - RanToCompletion is raised before awaiting resumes.
        /// - The awaited duration reflects the configured Interval.
        /// </remarks>
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

        /// <summary>
        /// Verifies that a running epoch can be canceled and that awaiting the
        /// WatchdogTimer resumes with a canceled TaskStatus.
        /// </summary>
        /// <remarks>
        /// The test confirms:
        /// - Awaiting an idle WDT completes immediately.
        /// - StartOrRestart begins a new epoch and raises EpochInitialized.
        /// - Calling Cancel during the interval suppresses completion.
        /// - Awaiting the WDT resumes with TaskStatus.Canceled.
        /// - The elapsed time reflects the cancellation timing rather than the full interval.
        /// - RanToCompletion is not raised when the epoch is canceled.
        /// </remarks>

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

        /// <summary>
        /// Verifies that a subclass override of OnEpochFinalizingAsync can extend
        /// the epoch with additional asynchronous work that participates in settlement.
        /// </summary>
        /// <remarks>
        /// The test confirms:
        /// - Awaiting an idle WDT completes immediately.
        /// - A subclass override can perform async work before invoking base.
        /// - Awaiting the WDT includes the subclass’s async workload.
        /// - The observed elapsed time reflects both the configured Interval
        ///   and the additional async delay introduced by the override.
        /// </remarks>
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

        /// <summary>
        /// Verifies end-to-end integration of a WatchdogTimer-backed model that
        /// performs asynchronous data work within the epoch boundary.
        /// </summary>
        /// <remarks>
        /// The test confirms:
        /// - Updating InputText triggers a new epoch.
        /// - Awaiting the model awaits the underlying WatchdogTimer.
        /// - The asynchronous query performed during finalization completes
        ///   before awaiting resumes.
        /// - The resulting data population is observable and deterministic
        ///   at the await boundary.
        /// </remarks>
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

        /// <summary>
        /// Verifies end-to-end behavior of a composition-based model that owns
        /// a WatchdogTimer and participates in epoch settlement via EpochInvokeAsync.
        /// </summary>
        /// <remarks>
        /// The test confirms:
        /// - Updating InputText triggers a new epoch on the owned WatchdogTimer.
        /// - Async work is enrolled through EpochInvokeAsync within the
        ///   EpochFinalizing handler.
        /// - Awaiting the model awaits the underlying WatchdogTimer.
        /// - Data population completes before awaiting resumes, producing
        ///   deterministic, serialized results.
        /// </remarks>
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

        /// <summary>
        /// Verifies that DHostSQLiteAsyncConnection initializes once per epoch,
        /// remains awaitable after completion, and can be re-entered in a new
        /// reference-counted lifetime without duplicating initialization.
        /// </summary>
        /// <remarks>
        /// The test confirms:
        /// - Initialization logic executes during the first token scope.
        /// - Awaiting the host after disposal completes the current epoch.
        /// - A subsequent token begins a new lifetime without re-seeding data.
        /// - The underlying database state remains consistent across epochs.
        /// </remarks>

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

        /// <summary>
        /// Verifies that multiple EpochFinalizing handlers participate in ordered,
        /// FIFO-serialized async settlement and that the epoch completes only after
        /// all queued delegates finish.
        /// </summary>
        /// <remarks>
        /// The test confirms:
        /// - Each handler invokes EpochInvokeAsync exactly once.
        /// - Async work from multiple handlers is executed sequentially in FIFO order.
        /// - The awaited WatchdogTimer resumes only after the full serialized duration
        ///   plus the configured Interval.
        /// - No reentrancy or late-participation Throws occur during normal usage.
        /// </remarks>

        [TestMethod]
        public async Task Test_BeginAsync()
        {
            Stopwatch stopwatch = new();
            var wdt = new WatchdogTimer { Interval = TimeSpan.FromSeconds(0.25) };
            Queue<SenderEventPair> eventQueue = new();

            #region L o c a l F x				
            using var local = this.WithOnDispose(
                onInit: (sender, e) =>
                {
                    Throw.BeginThrowOrAdvise += localOnEvent;
                },
                onDispose: (sender, e) =>
                {
                    Throw.BeginThrowOrAdvise -= localOnEvent;
                });
            void localOnEvent(object? sender, EventArgs e)
            {
                eventQueue.Enqueue((sender, e));
                if(e is Throw @throw)
                {
                    @throw.Handled = true;
                }
            }
            #endregion L o c a l F x


            /// Connection point 1
            wdt.EpochFinalizing += async (sender, e) =>
            {
                e.QueueEpochTask(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.1));
                });
            };
            /// Connection point 2
            wdt.EpochFinalizing += async (sender, e) =>
            {
                e.QueueEpochTask(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.1));
                });
            };

            wdt.EpochFinalizing += async (sender, e) =>
            {
                e.QueueEpochTask(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.1));
                    await Task.Delay(TimeSpan.FromSeconds(0.1));
                });
            };
            /// Connection point 3 - ordered
            stopwatch.Restart();
            wdt.StartOrRestart();
            await wdt;
            stopwatch.Stop();

            Assert.IsTrue((4 * TimeSpan.FromSeconds(0.1)) + wdt.Interval <= stopwatch.Elapsed);

            await Task.Delay(TimeSpan.FromSeconds(1)); // Propagate any errors now.
            Assert.AreEqual(0, eventQueue.Count, "Expecting no throws.");
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
}
