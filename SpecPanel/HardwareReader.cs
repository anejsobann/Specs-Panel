using LibreHardwareMonitor.Hardware;

namespace SpecPanel;

internal sealed record Metric(string Group, string Device, string Name, float? Value, string Unit);

internal sealed class HardwareReader : IDisposable
{
    private readonly Computer computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsStorageEnabled = true,
        IsMotherboardEnabled = true
    };

    private bool opened;

    public List<Metric> Read()
    {
        if (!opened)
        {
            computer.Open();
            opened = true;
        }

        var metrics = new List<Metric>();
        foreach (var device in computer.Hardware)
        {
            var group = device.HardwareType switch
            {
                HardwareType.Cpu => "CPU",
                HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia => "GPU",
                HardwareType.Memory => "RAM",
                HardwareType.Storage => "Diski",
                HardwareType.Motherboard => "Matična plošča",
                _ => null
            };

            if (group is not null)
                ReadDevice(device, group, device.Name, metrics);
        }

        return metrics;
    }

    private static void ReadDevice(IHardware device, string group, string name, List<Metric> metrics)
    {
        device.Update();
        var count = metrics.Count;
        foreach (var sensor in device.Sensors)
        {
            var unit = sensor.SensorType switch
            {
                SensorType.Temperature => "°C",
                SensorType.Load => "%",
                SensorType.Fan => "RPM",
                SensorType.Clock => "MHz",
                SensorType.Power => "W",
                SensorType.Data => "GB",
                SensorType.SmallData => "MB",
                _ => null
            };

            if (unit is not null)
            {
                var value = sensor.Value;
                if (value is float measured && (!float.IsFinite(measured) ||
                    (sensor.SensorType == SensorType.Temperature && group is "CPU" or "GPU" && measured <= 0)))
                    value = null;
                metrics.Add(new(group, name, sensor.Name, value, unit));
            }
        }

        foreach (var child in device.SubHardware)
            ReadDevice(child, group, name, metrics);

        if (count == metrics.Count)
            metrics.Add(new(group, name, "Senzorji niso na voljo", null, ""));
    }

    public void Dispose() => computer.Close();
}
