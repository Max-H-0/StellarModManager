using StellarModManager.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StellarModManager.Services;

public sealed record ModPackage(string Folder, InstalledModInfo Mod);

public sealed class ModPackageCache(string appDirectory)
{
    private readonly string _library = Path.Combine(appDirectory, "Library");
    private readonly string _cache = Path.Combine(appDirectory, "Data", "Packages");


    public static string DirectoryKey(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    public ModPackage? FindPackage(string id, string? version = null)
    {
        var root = Path.Combine(_cache, DirectoryKey(id.ToUpperInvariant()));
        string[] candidates;

        if (version != null)
        {
            candidates = [ Path.Combine(root, DirectoryKey(version)) ];
        }
        else 
        {
            candidates = Directory.Exists(root) ? Directory.GetDirectories(root) : [];
        }

        // TODO: please extract into local variable(s) with descriptive name(s)
        if (id.Length > 0 && id is not "." and not ".." && id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            !id.Contains('/') && !id.Contains('\\') && !Path.IsPathRooted(id))
        {
            candidates = candidates
                .Append(Path.Combine(_library, id))
                .ToArray();
        }

        return candidates
            .Select(TryReadPackage)
            .OfType<ModPackage>()
            .Where(p => string.Equals(p.Mod.Id, id, StringComparison.OrdinalIgnoreCase) && (version == null || p.Mod.Version == version))
            .OrderByDescending(p => Version.TryParse(p.Mod.Version, out var parsed) ? parsed : null)
            .FirstOrDefault();
    }

    public static ModPackage? ReadPackage(string folder)
    {
        var metadataPath = Path.Combine(folder, "mod.json");
        if (!File.Exists(metadataPath)) return null;

        try
        {
            var metedata = File.ReadAllText(metadataPath);

            if (JsonSerializer.Deserialize<InstalledModInfo>(metedata) is not InstalledModInfo mod) return null;

            return new ModPackage(folder, mod);
        }
        catch
        {
            return null;
        }
    }
    
    public async Task<ModPackage> GetAsync(OnlineModInfo mod, ModRepositoryService repository, IProgress<double> progress)
    {
        var id = mod.Id;
        var version = mod.Version;
        var name = mod.Name;
        var dependencies = mod.Dependencies?.ToList() ?? [];

        if (FindPackage(id, version) is ModPackage existingPackage) return existingPackage;


        var root = Path.Combine(_cache, DirectoryKey(id.ToUpperInvariant()));
        var destination = Path.Combine(root, DirectoryKey(version));
        Directory.CreateDirectory(root);

        var work = Directory.CreateTempSubdirectory("stellar-package-").FullName;
        var staging = Path.Combine(_cache, $".package-{Guid.NewGuid():N}");
        try
        {
            var zip = Path.Combine(work, "package.zip");
            await repository.DownloadModAsync(mod.DownloadUrl, zip, progress);
            await Task.Run(() => ModInstallerService.InstallAsync(zip, staging));

            var package = ReadPackage(staging);
            if (package is null || !string.Equals(package.Mod.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The downloaded package does not match {name}.");
            }

            package.Mod.Version = version;
            if (package.Mod.Dependencies is not { Count: > 0 })
            {  
                package.Mod.Dependencies = dependencies;
            }

            File.WriteAllText(Path.Combine(staging, "mod.json"), JsonSerializer.Serialize(package.Mod));
            try
            {
                Directory.Move(staging, destination);
            }
            catch (IOException) when (Directory.Exists(destination)) {}

            return ReadPackage(destination) ?? throw new InvalidDataException("The cached package has no metadata.");
        }
        finally
        {
            ProfileFiles.TryDeleteDirectory(staging);
            ProfileFiles.TryDeleteDirectory(work);
        }
    }


    private static ModPackage? TryReadPackage(string folder)
    {
        try
        {
            return ReadPackage(folder);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
