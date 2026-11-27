using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    private const string ReleaseUrl = "https://github.com/anejsobann/Specs-Panel/releases/latest/download/SpecsPanel-win-x64.zip";

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var root = AppDomain.CurrentDomain.BaseDirectory;
        var installed = Path.Combine(root, "Windows App");
        try
        {
            if (!Launch(root) && !Launch(installed))
                DownloadAndLaunch(root, installed);
        }
        catch (Exception error)
        {
            ShowError(error);
        }
    }

    private static bool Launch(string package)
    {
        var directory = Path.Combine(package, "App");
        var executable = Path.Combine(directory, "SpecsPanel.exe");
        if (!File.Exists(executable))
            return false;

        using (Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = true }))
            return true;
    }

    private static void DownloadAndLaunch(string root, string installed)
    {
        using (var form = new Form
        {
            Text = "Specs Panel",
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
            ClientSize = new Size(360, 90),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ControlBox = false
        })
        {
            form.Controls.Add(new Label
            {
                Text = "Downloading Specs Panel for Windows…",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            form.Shown += async (sender, args) =>
            {
                try
                {
                    await Task.Run(() => Download(root, installed));
                    if (!Launch(installed))
                        throw new IOException("SpecsPanel.exe is missing from the extracted release.");
                }
                catch (Exception error)
                {
                    ShowError(error);
                }
                finally
                {
                    form.Close();
                }
            };
            Application.Run(form);
        }
    }

    private static void Download(string root, string installed)
    {
        var staging = Path.Combine(root, ".specs-panel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var zip = Path.Combine(staging, "SpecsPanel.zip");
            var package = Path.Combine(staging, "Package");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var client = new WebClient())
                client.DownloadFile(ReleaseUrl, zip);
            ZipFile.ExtractToDirectory(zip, package);
            if (!File.Exists(Path.Combine(package, "App", "SpecsPanel.exe")))
                throw new IOException("The Windows ZIP is missing SpecsPanel.exe.");

            try { Directory.Move(package, installed); }
            catch (IOException)
            {
                if (!File.Exists(Path.Combine(installed, "App", "SpecsPanel.exe")))
                    throw;
            }
        }
        finally
        {
            Directory.Delete(staging, true);
        }
    }

    private static void ShowError(Exception error)
    {
        MessageBox.Show("Could not start Specs Panel.\n\n" + error.Message, "Specs Panel",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
