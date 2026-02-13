using System.Diagnostics;
using System.Text;

namespace IVSoftware.Portable.MSTest;

[TestClass]
public class TestClass_ReadMeClaims
{

    [TestMethod]
    public async Task Test_SimpleDebounce()
    {
        TaskCompletionSource tcsTaskStarted = new();
        StringBuilder inputText = new();
        Stopwatch stopwatch = new(); // Measure epoch for test.
        var wdt = new WatchdogTimer(
            defaultInitialAction: () => 
            { 
                inputText.Clear();
                stopwatch.Restart();
                tcsTaskStarted.TrySetResult();
            }, 
            defaultCompleteAction: () => 
            { 
                Debug.WriteLine($"@{stopwatch.Elapsed.TotalSeconds} Settled Text: {inputText}");
            })
        {
            Interval = TimeSpan.FromSeconds(0.5)
        };
        // Simulate keystrokes that would normally occur on the UI thread.        
        _ = Task.Run(async () =>
        {
            foreach (var c in new[] { 'g', 'r', 'e', 'e', 'n'})
            {
                await Task.Delay(TimeSpan.FromSeconds(0.25));
                wdt.StartOrRestart();
                inputText.Append(c);
            }
        });

        await tcsTaskStarted.Task;
        await wdt;
    }
}
