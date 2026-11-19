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
        Application.Run(new Dashboard(args));
    }    
}
