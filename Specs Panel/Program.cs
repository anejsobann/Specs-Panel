using System.Globalization;

namespace SpecsPanel;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var english = CultureInfo.GetCultureInfo("en-GB");
        CultureInfo.DefaultThreadCurrentCulture = english;
        CultureInfo.DefaultThreadCurrentUICulture = english;
        ApplicationConfiguration.Initialize();
        try
        {
            if (ReleaseUpdate.TryStartInstalled(args))
                return;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception)
        {
            System.Diagnostics.Debug.WriteLine($"Could not start the installed update: {error.Message}");
        }
        Application.Run(new Dashboard(args));
    }    
}
