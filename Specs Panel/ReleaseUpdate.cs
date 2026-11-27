using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace SpecsPanel;

internal static class ReleaseUpdate
{
    private const string Repository = "anejsobann/Specs-Panel";
    private const string PackageName = "SpecsPanel-win-x64";
    private static readonly Version Current = typeof(ReleaseUpdate).Assembly.GetName().Version!;
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(3),
        MaxResponseContentBufferSize = 128 * 1024 * 1024,
        DefaultRequestHeaders = { { "User-Agent", "SpecsPanel" } }
    };

    internal sealed record Manifest(string Version, Dictionary<string, string> Files);

    private static string? UpdateDirectory
    {
        get
        {
            var app = new DirectoryInfo(AppContext.BaseDirectory);
            var root = app.Parent?.Name == "Updates" ? app.Parent.Parent : app.Parent;
            return root is not null && (File.Exists(Path.Combine(root.FullName, "SpecsPanel.exe")) ||
                File.Exists(Path.Combine(root.FullName, "Start SpecsPanel.cmd")))
                ? Path.Combine(root.FullName, "Updates")
                : null;
        }
    }

    private static string CurrentVersion => Current.ToString(3);

    public static bool TryStartInstalled(string[] arguments)
    {
        var updates = UpdateDirectory;
        if (!Directory.Exists(updates))
            return false;

        var next = Directory.EnumerateDirectories(updates)
            .Select(path => (Path: path, Version: ParseVersion(Path.GetFileName(path))))
            .Where(candidate => candidate.Version > ParseVersion(CurrentVersion))
            .OrderByDescending(candidate => candidate.Version)
            .FirstOrDefault();
        return next.Path is not null && Start(next.Path, arguments);
    }

    public static async Task<Manifest?> CheckAsync(CancellationToken cancellationToken)
    {
        if (UpdateDirectory is null)
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        using var response = await Client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var release = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        var tag = release!.RootElement.GetProperty("tag_name").GetString();
        var version = tag?.StartsWith('v') == true ? ParseVersion(tag[1..]) : null;
        if (version <= ParseVersion(CurrentVersion) || version is null)
            return null;

        var assets = release.RootElement.GetProperty("assets").EnumerateArray()
            .Select(asset => asset.GetProperty("name").GetString()).ToHashSet();
        if (!assets.Contains(DeltaName) || !assets.Contains($"{PackageName}.json"))
            return null;

        var manifest = await Client.GetFromJsonAsync<Manifest>(
            AssetUrl(version, $"{PackageName}.json"), cancellationToken);
        return manifest?.Version == version.ToString() && manifest.Files.ContainsKey("SpecsPanel.exe")
            ? manifest : null;
    }

    public static async Task<bool> InstallAsync(Manifest manifest, string[] arguments, CancellationToken token)
    {
        var updates = UpdateDirectory ?? throw new InvalidOperationException("Release folder not found.");
        var version = ParseVersion(manifest.Version) ?? throw new InvalidDataException("Invalid update version.");
        var destination = Path.Combine(updates, version.ToString());
        var staging = Path.Combine(updates, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            if (!Directory.Exists(destination))
            {
                using var response = await Client.GetAsync(AssetUrl(version, DeltaName), token);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                foreach (var (path, hash) in manifest.Files)
                {
                    var target = ResolvePath(staging, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    var entry = archive.GetEntry(path);
                    await using var source = entry is null
                        ? File.OpenRead(ResolvePath(AppContext.BaseDirectory, path))
                        : entry.Open();
                    await using (var output = File.Create(target))
                        await source.CopyToAsync(output, token);
                    await using var verification = File.OpenRead(target);
                    if (!Convert.ToHexString(await SHA256.HashDataAsync(verification, token))
                        .Equals(hash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Update file failed verification: {path}");
                }

                token.ThrowIfCancellationRequested();
                try { Directory.Move(staging, destination); }
                catch (IOException) when (Directory.Exists(destination)) { }
            }

            return Start(destination, arguments);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    private static string DeltaName => $"{PackageName}-from-{CurrentVersion}.zip";
    private static string AssetUrl(Version version, string asset) =>
        $"https://github.com/{Repository}/releases/download/v{version}/{asset}";

    private static Version? ParseVersion(string value) =>
        Version.TryParse(value, out var version) && version.Build >= 0 && version.Revision < 0
            && version.ToString() == value ? version : null;

    private static string ResolvePath(string directory, string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path, root);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || path.Contains(':'))
            throw new InvalidDataException("Invalid update file path.");
        return fullPath;
    }

    private static bool Start(string directory, string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(directory, "SpecsPanel.exe")) { WorkingDirectory = directory };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start);
        return process is not null;
    }
}
