using System.IO;

namespace StellarModManager.Services;

public enum MelonLoaderStatus { Installed, NotInstalled }

public static class MelonLoaderDetector
{
    // Idk if this is a good way to check if MelonLoader is installed, but Idc.
    public static MelonLoaderStatus CheckInstallation(string gamePath)
    {
        bool folderExists = Directory.Exists(Path.Combine(gamePath, "MelonLoader"));
        bool dllExists = File.Exists(Path.Combine(gamePath, "version.dll"));
        bool isInstalled = folderExists && dllExists;

        return isInstalled ? MelonLoaderStatus.Installed : MelonLoaderStatus.NotInstalled;
    }
}