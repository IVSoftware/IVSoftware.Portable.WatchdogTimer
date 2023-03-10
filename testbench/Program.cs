using IVSoftware.Portable;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace testbench
{
    internal class Program
    {
        static void Main(string[] args)
        {
            runAsync();

            Console.ReadKey();
        }

        private static async void runAsync()
        {
            BringConsoleToFront();

            // Three second watchdog timer
            WatchdogTimer _wdt = new WatchdogTimer { Interval = TimeSpan.FromSeconds(3) };
            // System.Diagnostics.Stopwatch for testing purposes.
            Stopwatch _stopWatch = new Stopwatch();

            #region T E S T    A C T I O N
            Console.WriteLine("T E S T    E V E N T");
            _stopWatch.Start();
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(()=> MarkEmailAsRead(id: 1));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 2));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 3)); 
            // Should run to completion
            await Task.Delay(TimeSpan.FromSeconds(4));
            Console.WriteLine(_stopWatch.Elapsed + Environment.NewLine);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 10));
            await Task.Delay(TimeSpan.FromSeconds(2));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 20));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 30));
            // Should run to completion
            await Task.Delay(TimeSpan.FromSeconds(4));
            Console.WriteLine(_stopWatch.Elapsed + Environment.NewLine);

            #endregion T E S T    A C T I O N

            #region T E S T    E V E N T
            _wdt.RanToCompletion += onWdtRanToCompletion;

            Console.WriteLine("T E S T    A C T I O N");
            _stopWatch.Restart();
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(new CustomEventArgs(id: 1));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(new CustomEventArgs(id: 2));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(new CustomEventArgs(id: 3));
            // Should run to completion
            await Task.Delay(TimeSpan.FromSeconds(4));
            Console.WriteLine(_stopWatch.Elapsed + Environment.NewLine);


            Console.WriteLine(_stopWatch.Elapsed);
            _wdt.StartOrRestart(new CustomEventArgs(id: 10));
            await Task.Delay(TimeSpan.FromSeconds(2));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(new CustomEventArgs(id: 20));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(new CustomEventArgs(id: 30));
            // Should run to completion
            await Task.Delay(TimeSpan.FromSeconds(4));
            Console.WriteLine(_stopWatch.Elapsed + Environment.NewLine);
            #endregion T E S T    E V E N T

            #region T E S T    C A N C E L
            Console.WriteLine("T E S T    C A N C E L");
            _wdt.RanToCompletion += onWdtRanToCompletion;

            _stopWatch.Restart();
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(()=>MarkEmailAsRead(id: 1), new CustomEventArgs(id: 1));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 2), new CustomEventArgs(id: 2));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 3), new CustomEventArgs(id: 3));
            // Cancel event and allow enough time to otherwise complete.
            await Task.Delay(TimeSpan.FromSeconds(2));
            _wdt.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(2));
            Console.WriteLine(_stopWatch.Elapsed + Environment.NewLine);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 10), new CustomEventArgs(id: 10));
            await Task.Delay(TimeSpan.FromSeconds(2));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 20), new CustomEventArgs(id: 20));
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine(_stopWatch.Elapsed);

            _wdt.StartOrRestart(() => MarkEmailAsRead(id: 30), new CustomEventArgs(id: 30));
            // Cancel event and allow enough time to otherwise complete.
            await Task.Delay(TimeSpan.FromSeconds(2));
            _wdt.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(2));
            Console.WriteLine($"DONE {_stopWatch.Elapsed}" );
            #endregion T E S T    A C T I O N


            #region L o c a l F x
            void MarkEmailAsRead(int id)
            {
                Console.WriteLine($"Expired: {_stopWatch.Elapsed}");
                Console.WriteLine($"The email with the ID '{id}' has been marked read");
            }

            void onWdtRanToCompletion(object sender, EventArgs e)
            {
                if (e is CustomEventArgs ePlus)
                {
                    Console.WriteLine($"Expired: {_stopWatch.Elapsed}");
                    Console.WriteLine($"The email with the ID '{ePlus.Id}' has been marked read");
                }
            }

            void BringConsoleToFront()
            {
                SetForegroundWindow(GetConsoleWindow());
            }
            #endregion L o c a l F x
        }
            
        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
    class CustomEventArgs : EventArgs
    {
        public CustomEventArgs(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }
}
