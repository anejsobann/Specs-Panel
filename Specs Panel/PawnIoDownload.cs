using System.Security.Cryptography;

namespace SpecsPanel;

internal static class PawnIoDownload
{
    public const string ReleasesUrl = "https://github.com/namazso/PawnIO.Setup/releases";
    private const string Version = "2.2.0";
    private const string Sha256 = "1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(45),
        MaxResponseContentBufferSize = 8 * 1024 * 1024
    };

    public static async Task<string> GetInstallerAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecsPanel", "Downloads");
        Directory.CreateDirectory(directory);
        var installer = Path.Combine(directory, $"PawnIO_setup-{Version}.exe");
        if (File.Exists(installer))
        {
            await using var cached = File.OpenRead(installer);
            if (Convert.ToHexString(await SHA256.HashDataAsync(cached, cancellationToken)) == Sha256)
                return installer;
        }

        // The release asset is about 3.25 MB. Bound the response before buffering.
        var url = $"{ReleasesUrl}/download/{Version}/PawnIO_setup.exe";
        using var response = await Client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != Sha256)
            throw new InvalidDataException("The PawnIO installer checksum did not match.");

        var pending = $"{installer}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(pending, bytes, cancellationToken);
            File.Move(pending, installer, overwrite: true);
            return installer;
        }
        finally
        {
            File.Delete(pending);
        }
    }
}
