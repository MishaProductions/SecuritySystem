using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritySystem.Utils
{
    /// <summary>
    /// https://stackoverflow.com/a/65573713
    /// </summary>
    internal static class CpuMemoryMetrics4LinuxUtils
    {
        private const int DigitsInResult = 2;
        private static long totalMemoryInKb;

        /// <summary>
        /// Get the system overall CPU usage percentage.
        /// </summary>
        /// <returns>The percentange value with the '%' sign. e.g. if the usage is 30.1234 %,
        /// then it will return 30.12.</returns>
        public static double GetOverallCpuUsagePercentage()
        {
            // refer to https://stackoverflow.com/questions/59465212/net-core-cpu-usage-for-machine
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetProcesses().Sum(a => a.TotalProcessorTime.TotalMilliseconds);

            System.Threading.Thread.Sleep(500);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetProcesses().Sum(a => a.TotalProcessorTime.TotalMilliseconds);

            var cpuUsedMs = endCpuUsage - startCpuUsage;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            return Math.Round(cpuUsageTotal * 100, DigitsInResult);
        }

        /// <summary>
        /// Get the system overall memory usage percentage.
        /// </summary>
        /// <returns>The percentange value with the '%' sign. e.g. if the usage is 30.1234 %,
        /// then it will return 30.12.</returns>
        public static double GetOccupiedMemoryPercentage()
        {
            var totalMemory = GetTotalMemoryInKb();
            var usedMemory = GetUsedMemoryForAllProcessesInKb();

            var percentage = usedMemory * 100 / totalMemory;
            return Math.Round(percentage, DigitsInResult);
        }

        private static double GetUsedMemoryForAllProcessesInKb()
        {
            var totalAllocatedMemoryInBytes = Process.GetProcesses().Sum(a => a.PrivateMemorySize64);
            return totalAllocatedMemoryInBytes / 1024.0;
        }

        private static long GetTotalMemoryInKb()
        {
            // only parse the file once
            if (totalMemoryInKb > 0)
            {
                return totalMemoryInKb;
            }

            string path = "/proc/meminfo";
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            using (var reader = new StreamReader(path))
            {
                string? line = string.Empty;
                while (!string.IsNullOrWhiteSpace(line = reader.ReadLine()))
                {
                    if (line.Contains("MemTotal", StringComparison.OrdinalIgnoreCase))
                    {
                        // e.g. MemTotal:       16370152 kB
                        var parts = line.Split(':');
                        var valuePart = parts[1].Trim();
                        parts = valuePart.Split(' ');
                        var numberString = parts[0].Trim();

                        var result = long.TryParse(numberString, out totalMemoryInKb);
                        return result ? totalMemoryInKb : throw new Exception($"Cannot parse 'MemTotal' value from the file {path}.");
                    }
                }

                throw new Exception($"Cannot find the 'MemTotal' property from the file {path}.");
            }
        }
    }
}
