using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SpecsPanel;

internal sealed record ProcessUsage(int Id, string Name, double? Cpu, double MemoryMiB, double? IoMiBPerSecond);

internal sealed class ProcessReader
{
    private const double BytesPerMiB = 1024 * 1024;
    private Dictionary<int, Sample> previous = new();
    private sealed record Sample(DateTime Start, long Timestamp, TimeSpan CpuTime, IoCounters? Io);

    public List<ProcessUsage> Read()
    {
        var current = new Dictionary<int, Sample>();
        var rows = new List<ProcessUsage>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == 0)
                    continue;
                try
                {
                    var name = process.ProcessName;
                    var memory = process.WorkingSet64 / BytesPerMiB;
                    var sample = ReadSample(process);
                    double? cpu = null;
                    double? io = null;
                    if (sample is not null)
                    {
                        current[process.Id] = sample;
                        if (previous.TryGetValue(process.Id, out var prior) && prior.Start == sample.Start)
                        {
                            var seconds = Stopwatch.GetElapsedTime(prior.Timestamp, sample.Timestamp).TotalSeconds;
                            if (seconds > 0)
                            {
                                cpu = Math.Clamp(100 * (sample.CpuTime - prior.CpuTime).TotalSeconds
                                    / seconds / Environment.ProcessorCount, 0, 100);
                                if (sample.Io is { } next && prior.Io is { } last &&
                                    next.ReadBytes >= last.ReadBytes && next.WriteBytes >= last.WriteBytes)
                                {
                                    io = ((double)(next.ReadBytes - last.ReadBytes) + (next.WriteBytes - last.WriteBytes))
                                        / seconds / BytesPerMiB;
                                }
                            }
                        }
                    }
                    rows.Add(new(process.Id, name, cpu, memory, io));
                }
                catch (Exception error) when (error is InvalidOperationException or Win32Exception)
                {
                    // A process can exit or become inaccessible during enumeration.
                }
            }
        }

        previous = current;
        return rows;
    }

    private static Sample? ReadSample(Process process)
    {
        const uint queryLimitedInformation = 0x1000;
        using var handle = OpenProcess(queryLimitedInformation, false, process.Id);
        if (handle.IsInvalid || !GetProcessTimes(handle, out var creation, out _, out var kernel, out var user))
            return null;
        IoCounters? io = GetProcessIoCounters(handle, out var counters) ? counters : null;
        return new(DateTime.FromFileTimeUtc(creation), Stopwatch.GetTimestamp(), TimeSpan.FromTicks(kernel + user), io);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperations;
        public ulong WriteOperations;
        public ulong OtherOperations;
        public ulong ReadBytes;
        public ulong WriteBytes;
        public ulong OtherBytes;
    }

    [DllImport("kernel32.dll")]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle handle, out long creation, out long exit, out long kernel, out long user);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(SafeProcessHandle handle, out IoCounters counters);
}
