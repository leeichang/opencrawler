using System.Runtime.InteropServices;

namespace OpenCrawler.Core.Infrastructure;

public static class AppPaths
{
    public static string ConfigDirectory
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "openCrawler");
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Application Support", "openCrawler");
            }
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdg))
                return Path.Combine(xdg, "openCrawler");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "openCrawler");
        }
    }

    public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

    public static string DbFilePath(string storageRoot) => Path.Combine(storageRoot, "opencrawler.db");

    public static string LogDirectory(string storageRoot) => Path.Combine(storageRoot, "logs");
}
