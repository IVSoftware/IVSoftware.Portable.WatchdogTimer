using IVSoftware.Portable.Xml.Linq.XBoundObject.Modeling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IVSoftware.Portable.MSTest
{
    static class MSTestExtensions
    {
        public static TimeSpan ExecStartOrRestartLoop(this WatchdogTimer @this, int loopN = 10, TimeSpan? delay = null)
        {
            Assert.IsTrue(
                loopN >= 1,
                "Expecting loopN must be greater than or equal to 1.");

            delay ??= TimeSpan.FromSeconds(0.25);
            TimeSpan dwell = @this.Interval + ((loopN - 1) * (TimeSpan)delay);

            _ = localStartOrRestart();
            async Task localStartOrRestart()
            {
                for (int i = 0; i < loopN -1; i++)
                {
                    @this.StartOrRestart();
                    await Task.Delay((TimeSpan)delay);
                    Assert.IsTrue(@this.Running);
                }
                @this.StartOrRestart();
                Assert.IsTrue(@this.Running);
            }
            return dwell;
        }
        public static TimeSpan ExecStartOrRestartLoop(this WatchdogTimer @this, string inputString, TimeSpan? delay = null)
            => @this.ExecStartOrRestartLoop(inputString.Length, delay);
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
