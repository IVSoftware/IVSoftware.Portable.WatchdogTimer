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
        var wdt = new WatchdogTimer(
            defaultInitialAction: () =>
            {
                tcsSimStarted.TrySetResult();
                stopwatch.Restart();
            },
            defaultCompleteAction: () =>
            {
                Debug.WriteLine($"@{stopwatch.Elapsed.TotalSeconds} Settled Text: {inputText}");
                tcsSimStarted = new(); // Housekeeping - sets up the next potential test case.
            })
        {
            Interval = TimeSpan.FromSeconds(0.5)
        };
        // Simulate keystrokes that would normally occur on the UI thread.
        // - Do not await here. This is just a burst of keystrokes in the wild.
        _ = Task.Run(async () =>
        {
            inputText.Clear();
            foreach (var c in new[] { 'g', 'r', 'e', 'e', 'n' })
            {
                await Task.Delay(TimeSpan.FromSeconds(0.25));
                inputText.Append(c);
                wdt.StartOrRestart();
            }
        });

        // Without awaiting, this test would return immediately
        // (the fire-and-forget input simulation may not execute at all).
        await tcsSimStarted.Task;
        await wdt; // Await deterministic epoch settlement.
    }

    [TestMethod]
    public async Task Test_OverridableAsyncHook()
    {
        TaskCompletionSource tcsSimStarted = new(); // Will ensure that the test enters the simulation.
        Stopwatch stopwatch = new(); // Measure epoch for test.
        TextBoxAwaitable textBox = new();
        textBox.EpochInitialized += (sender, e) =>
        {
            stopwatch.Start();
            tcsSimStarted.TrySetResult();
        };

        // Simulate keystrokes that would normally occur on the UI thread.
        // - Do not await here. This is just a burst of keystrokes in the wild.
        _ = Task.Run(async () =>
        {
            textBox.Clear();
            StringBuilder sb = new ();
            foreach (var c in new[] { 'g', 'r', 'e', 'e', 'n' })
            {
                await Task.Delay(TimeSpan.FromSeconds(0.25));
                sb.Append(c);
                textBox.Text = sb.ToString();
            }
        });

        // Without awaiting, this test would return immediately
        // (the fire-and-forget input simulation may not execute at all).
        await tcsSimStarted.Task;
        await textBox; // Await deterministic epoch settlement.
        stopwatch.Stop();
        tcsSimStarted = new(); // Ready for more cases.
        { }
    }
}
