using IVSoftware.Portable.MSTest.Models;
using System.Diagnostics;
using System.Text;

namespace IVSoftware.Portable.MSTest;

[TestClass]
public class TestClass_ReadMeClaims
{
    [TestMethod]
    public async Task Test_SimpleDebounce()
    {
        TaskCompletionSource tcsSimStarted = new(); // Will ensure that the test enters the simulation.
        StringBuilder inputText = new();
        Stopwatch stopwatch = new(); // Measure epoch for test.
        TimeSpan 
            inputSettleInterval = TimeSpan.FromSeconds(0.5),
            inputCharacterPeriod = TimeSpan.FromSeconds(0.25);

        var wdt = new WatchdogTimer(
            defaultInitialAction: () =>
            {
                tcsSimStarted.TrySetResult();
                stopwatch.Restart();
            },
            defaultCompleteAction: () =>
            {
                Debug.WriteLine($"@{stopwatch.Elapsed.TotalSeconds} Settled Text: {inputText}");
            })
        {
            Interval = inputSettleInterval
        };
        // Simulate keystrokes that would normally occur on the UI thread.
        // - Do not await here. This is just a burst of keystrokes in the wild.
        _ = Task.Run(async () =>
        {
            inputText.Clear();
            foreach (var c in new[] { 'g', 'r', 'e', 'e', 'n' })
            {
                await Task.Delay(inputCharacterPeriod);
                inputText.Append(c);
                wdt.StartOrRestart();
            }
        });

        // Without awaiting, this test would return immediately
        // (the fire-and-forget input simulation may not execute at all).
        await tcsSimStarted.Task;
        await wdt; // Await deterministic epoch settlement.
        stopwatch.Stop();

        var expected = wdt.EpochTimeSpanExpected("green", inputCharacterPeriod);
        Assert.IsTrue(TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds) >= expected, "Expecting a minimum settle time.");
        Assert.IsTrue(TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds) <= expected + TimeSpan.FromSeconds(0.5), "Expecting a maximum settle time window.");
    }

    [TestMethod]
    public async Task Test_OverridableAsyncHook()
    {
        TaskCompletionSource tcsSimStarted = new(); // Will ensure that the test enters the simulation.
        Stopwatch stopwatch = new(); // Measure epoch for test.
        TimeSpan
            inputSettleInterval = TimeSpan.FromSeconds(0.5),
            inputCharacterPeriod = TimeSpan.FromSeconds(0.25);

        var textBox = new TextBoxAwaitableBaseClass {  Interval = inputSettleInterval };

        textBox.EpochInitialized += (sender, e) =>
        {
            stopwatch.Start();
            tcsSimStarted.TrySetResult();
        };

        await subtestSimulateTyping("green");

        #region S U B T E S T S 
        async Task subtestSimulateTyping(string inputText)
        {
            // Simulate keystrokes that would normally occur on the UI thread.
            // - Do not await here. This is just a burst of keystrokes in the wild.
            _ = Task.Run(async () =>
            {
                textBox.Clear();
                StringBuilder sb = new();

                int i = 0;
                while (i < inputText.Length - 1)
                {
                    sb.Append(inputText[i++]);
                    textBox.Text = sb.ToString();
                    await Task.Delay(inputCharacterPeriod);
                }
                sb.Append(inputText[i]);
                textBox.Text = sb.ToString();
            });

            // Without awaiting, this test would return immediately
            // (the fire-and-forget input simulation may not execute at all).
            await tcsSimStarted.Task;
            await textBox; // Await deterministic epoch settlement.
            stopwatch.Stop();

            var expected = textBox.EpochTimeSpanExpected("green", inputCharacterPeriod);
            Assert.IsTrue(TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds) >= expected, "Expecting a minimum settle time.");
            Assert.IsTrue(TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds) <= expected + TimeSpan.FromSeconds(0.5), "Expecting a maximum settle time window.");

            tcsSimStarted = new(); // RESET TCS - Ready for next subtest.
        }
        #endregion S U B T E S T S
    }



    [TestMethod]
    public async Task Test_OverridableAsyncHookSubclass()
    {
        TaskCompletionSource tcsSimStarted = new(); // Will ensure that the test enters the simulation.
        Stopwatch stopwatch = new(); // Measure epoch for test.
        TimeSpan
            inputSettleInterval = TimeSpan.FromSeconds(0.5),
            inputCharacterPeriod = TimeSpan.FromSeconds(0.25),
            asyncFinalizationPeriod = TimeSpan.FromSeconds(1);

        // Different!
        var textBox = new TextBoxAwaitable{ Interval = inputSettleInterval };

        textBox.EpochInitialized += (sender, e) =>
        {
            stopwatch.Start();
            tcsSimStarted.TrySetResult();
        };

        await subtestSimulateTyping("green");

        #region S U B T E S T S 
        async Task subtestSimulateTyping(string inputText)
        {
            // Simulate keystrokes that would normally occur on the UI thread.
            // - Do not await here. This is just a burst of keystrokes in the wild.
            _ = Task.Run(async () =>
            {
                textBox.Clear();
                StringBuilder sb = new();

                int i = 0;
                while (i < inputText.Length - 1)
                {
                    sb.Append(inputText[i++]);
                    textBox.Text = sb.ToString();
                    await Task.Delay(inputCharacterPeriod);
                }
                sb.Append(inputText[i]);
                textBox.Text = sb.ToString();
            });

            // Without awaiting, this test would return immediately
            // (the fire-and-forget input simulation may not execute at all).
            await tcsSimStarted.Task;
            await textBox; // Await deterministic epoch settlement.
            stopwatch.Stop();

            // Different
            var expected = textBox.EpochTimeSpanExpected("green", inputCharacterPeriod) + asyncFinalizationPeriod;

            Assert.IsTrue(TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds) >= expected, "Expecting a minimum settle time.");
            Assert.IsTrue(TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds) <= expected + TimeSpan.FromSeconds(0.5), "Expecting a maximum settle time window.");

            tcsSimStarted = new(); // RESET TCS - Ready for next subtest.
        }
        #endregion S U B T E S T S
    }
}
