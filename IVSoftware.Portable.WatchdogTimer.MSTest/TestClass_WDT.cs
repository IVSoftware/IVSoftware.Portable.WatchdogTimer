using IVSoftware.Portable.Disposable;
using IVSoftware.Portable.Xml.Linq.XBoundObject.Modeling;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace IVSoftware.Portable.MSTest
{
    [TestClass]
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

                for (var i = 0; i < 10; i++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.25));
                    Assert.IsTrue(uut.WDT.Running);
                    uut.WDT.StartOrRestart();
                }
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
