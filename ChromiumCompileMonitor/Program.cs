using System;
using System.Threading.Tasks;
using ChromiumCompileMonitor.Services;

namespace ChromiumCompileMonitor
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        static async Task Main(string[] args)
        {
            Console.WriteLine("Chromium Compile Progress Monitor");
            Console.WriteLine("=================================");
            Console.WriteLine();

            // Test the progress parser
            TestProgressParser();
            
            Console.WriteLine();
            Console.WriteLine("This is a console version for testing. To run the GUI version:");
            Console.WriteLine("1. Open this project in Visual Studio 2022");
            Console.WriteLine("2. Change the project settings to use Windows Forms");
            Console.WriteLine("3. Update the Program.cs to use the MainForm class");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void TestProgressParser()
        {
            var parser = new ProgressParser();
            
            Console.WriteLine("Testing Progress Parser with sample data:");
            Console.WriteLine("-----------------------------------------");

            var sampleLines = new[]
            {
                "[100/900] 5m30s",
                "[250/750] 12m45s", 
                "[500/500] 25m15s",
                "[750/1000] 1h5m30s",
                "[900/1000] 1h45m20s",
                "Invalid line - should be ignored",
                "[999/1000] 2h15m45s"
            };

            foreach (var line in sampleLines)
            {
                Console.WriteLine($"Input: {line}");
                var progress = parser.ParseLine(line);
                
                if (progress != null)
                {
                    Console.WriteLine($"  Compiled: {progress.CompiledBlocks}, Remaining: {progress.RemainingBlocks}");
                    Console.WriteLine($"  Total Blocks: {progress.TotalBlocks}");
                    Console.WriteLine($"  Elapsed Time: {progress.ElapsedTime}");
                    Console.WriteLine($"  Percentage: {progress.PercentageCompleted:F1}%");
                    Console.WriteLine($"  Time per Block: {progress.TimePerBlock:F2} seconds");
                    Console.WriteLine($"  Estimated Remaining: {progress.EstimatedTimeRemaining}");
                    Console.WriteLine($"  Estimated Total: {progress.EstimatedTotalTime}");
                    Console.WriteLine($"  Speed Trend: {progress.SpeedTrend}");
                }
                else
                {
                    Console.WriteLine("  Failed to parse - ignored");
                }
                Console.WriteLine();
            }
        }
    }
}