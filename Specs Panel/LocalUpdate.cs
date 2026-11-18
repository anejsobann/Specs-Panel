using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SpecsPanel;

internal static partial class LocalUpdate
{
    public static bool TryRestart(string[] viewArguments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        var releases = current.Parent;
        if (releases is null || releases.Name != "Local" || !ReleaseName().IsMatch(current.Name))
            return false;

        try
        {
            using var marker = File.Open(Path.Combine(releases.FullName, "latest.txt"), FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(marker);
            var next = reader.ReadToEnd().Trim();
            if (!ReleaseName().IsMatch(next) || string.CompareOrdinal(next, current.Name) <= 0)
                return false;

            var executable = Path.Combine(releases.FullName, next, "SpecsPanel.exe");
            if (!File.Exists(executable))
                return false;

            var start = new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)! };
            foreach (var argument in viewArguments)
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start);
            return process is not null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return false;
        }
    }

    [GeneratedRegex(@"\Arelease-[0-9]{21}-[a-f0-9]{32}\z")]
    private static partial Regex ReleaseName();
}
