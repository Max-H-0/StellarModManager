using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StellarModManager.Services;

public sealed class ProfileFileTransaction(string gamePath, string journalPath)
{
    public sealed class Journal
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public List<FileChange> Changes { get; set; } = [];
        public List<string> NewDirectories { get; set; } = [];
    }

    public sealed record FileChange(string RelativePath, bool IsDirectory, bool HadOriginal, bool HasReplacement);


    private readonly Journal _journal = new();
    
    public string WorkFolder => WorkPath(gamePath, _journal.Id);
    public Guid Id => _journal.Id;


    private static string WorkPath(string gamePath, Guid id) => Path.Combine(gamePath, $".stellar-profile-{id:N}");

    private static string Staged(string work, int index) => Path.Combine(work, $"incoming-{index}");
    private static string Backup(string work, int index) => Path.Combine(work, $"original-{index}");

    public void BeginStaging()
    {
        ProfileFiles.WriteJson(journalPath, _journal);
        Directory.CreateDirectory(WorkFolder);
    }

    public void Add(string relativePath, string? replacement, bool isDirectory = false)
    {
        var live = ProfileFiles.Resolve(gamePath, relativePath);
        var exists = Path.Exists(live);

        if (exists && Directory.Exists(live) != isDirectory)
        {
            throw new IOException($"A file or folder is blocking {relativePath}.");
        }

        if (replacement != null)
        {
            Directory.CreateDirectory(WorkFolder);
            var staged = Staged(WorkFolder, _journal.Changes.Count);

            if (isDirectory)
            {
                ProfileFiles.CopyDirectory(replacement, staged);
            }
            else
            {
                File.Copy(replacement, staged);
            }

            for (var parent = Path.GetDirectoryName(live)!; !Directory.Exists(parent); parent = Path.GetDirectoryName(parent)!)
            {
                var relativeParent = Path.GetRelativePath(gamePath, parent);

                if (!_journal.NewDirectories.Contains(relativeParent, ProfileFiles.PathComparer))
                {
                    _journal.NewDirectories.Add(relativeParent);
                }
            }
        }

        _journal.Changes.Add(new FileChange(relativePath, isDirectory, exists, replacement != null));
    }

    public void Apply()
    {
        Directory.CreateDirectory(WorkFolder);
        ProfileFiles.WriteJson(journalPath, _journal);

        for (int index = 0; index < _journal.Changes.Count; index++)
        {
            var change = _journal.Changes[index];
            var live = ProfileFiles.Resolve(gamePath, change.RelativePath);

            if (Path.Exists(live) != change.HadOriginal) throw new IOException($"{change.RelativePath} changed while the operation was being prepared.");


            if (change.HadOriginal)
            {
                Move(live, Backup(WorkFolder, index), change.IsDirectory);
            }

            if (change.HasReplacement)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(live)!);
                Move(Staged(WorkFolder, index), live, change.IsDirectory);
            }
        }
    }

    public static void Recover(string gamePath, string journalPath, Guid? committedId)
    {
        if (!File.Exists(journalPath)) return;

        var journal = ProfileFiles.ReadJson<Journal>(journalPath);
        var work = WorkPath(gamePath, journal.Id);

        if (committedId != journal.Id)
        {
            for (int index = journal.Changes.Count - 1; index >= 0; index--)
            {
                var change = journal.Changes[index];
                var backup = Backup(work, index);
                var staged = Staged(work, index);
                var hasBackup = Path.Exists(backup);

                if (change.HadOriginal && !hasBackup) continue;

                var live = ProfileFiles.Resolve(gamePath, change.RelativePath);
                if (change.HasReplacement && !Path.Exists(staged) && Path.Exists(live))
                {
                    Move(live, staged, change.IsDirectory);
                }

                if (hasBackup) 
                {
                    Move(backup, live, change.IsDirectory);
                }
            }

            foreach (var relative in journal.NewDirectories.OrderByDescending(path => path.Length))
            {
                var folder = ProfileFiles.Resolve(gamePath, relative);

                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    Directory.Delete(folder);
                }
            }
        }

        if (committedId != journal.Id) File.Delete(journalPath);
        ProfileFiles.TryDeleteDirectory(work);
        if (committedId == journal.Id) File.Delete(journalPath);
    }

    private static void Move(string from, string to, bool isDirectory)
    {
        if (isDirectory)
        {
            Directory.Move(from, to);
        }
        else
        {
            File.Move(from, to);
        }
    }
}

public static class ProfileFiles
{
    public const string Mods = "Mods";

    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;


    public static string Normalize(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        var pathHasInvalidParts = parts.Any
        (
            part => part.Length == 0 
                || part is "." or ".." 
                || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 
                || (OperatingSystem.IsWindows() && (part.EndsWith(' ') || part.EndsWith('.')))
        );

        if (pathHasInvalidParts) throw new InvalidDataException($"Invalid managed file path {path}.");

        if (string.Equals(parts[0], Mods, StringComparison.OrdinalIgnoreCase))
        {
            parts[0] = Mods;
        }

        return string.Join('/', parts);
    }

    public static bool IsInMods(string relativePath) => relativePath.StartsWith("Mods/", StringComparison.Ordinal);

    public static string Resolve(string root, string relativePath)
    {
        var current = root;

        foreach (var part in Normalize(relativePath).Split('/'))
        {
            current = Path.Combine(current, part);

            var entry = new FileInfo(current);
            if (entry.LinkTarget != null || (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0))
            {
                throw new IOException($"A symbolic link or reparse point is blocking {current}.");
            }
        }

        return current;
    }

    public static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        if (!Directory.Exists(source)) return;


        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            var target = Path.Combine(destination, entry.Name);

            if (entry.LinkTarget is string link)
            {
                if (entry is DirectoryInfo)
                {
                    Directory.CreateSymbolicLink(target, link);
                }
                else
                {
                    File.CreateSymbolicLink(target, link);
                }
            }
            else if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Unsupported reparse point at {entry.FullName}.");
            }
            else if (entry is DirectoryInfo)
            {
                CopyDirectory(entry.FullName, target);
            }
            else
            {
                File.Copy(entry.FullName, target);
            }
        }
    }

    public static T ReadJson<T>(string path) 
        where T : class 
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? throw new InvalidDataException($"Could not read {path}.");
    }

    public static void WriteJson<T>(string path, T value)
    {
        var temporary = path + $".{Guid.NewGuid():N}.tmp";

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, value);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException) {}
        catch (UnauthorizedAccessException) {}
    }
}
