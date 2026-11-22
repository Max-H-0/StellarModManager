using StellarModManager.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace StellarModManager.Services;

public static class ExternalModScanner
{
    public static List<InstalledModInfo> Scan(string gamePath, HashSet<string> managedPaths)
    {
        var result = new List<InstalledModInfo>();
        var modsFolder = Path.Combine(gamePath, ProfileFiles.Mods);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            foreach (var file in Directory.EnumerateFiles(modsFolder, "*.dll", options))
            {
                var relativePath = Path.GetRelativePath(gamePath, file).Replace(Path.DirectorySeparatorChar, '/');

                if (!managedPaths.Contains(relativePath)) 
                {
                    ReadAssembly(file, relativePath, result);
                }
            }
        }
        catch (IOException) {}
        catch (UnauthorizedAccessException) {}

        return result
            .OrderBy(mod => mod.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(mod => mod.ExternalPath, ProfileFiles.PathComparer)
            .ToList();
    }


    private static void ReadAssembly(string file, string relativePath, List<InstalledModInfo> result)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return;

            var metadata = pe.GetMetadataReader();
            if (!metadata.IsAssembly) return;

            var index = 0;
            foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
            {
                try
                {
                    var attribute = metadata.GetCustomAttribute(handle);
                    var readInfo = ReadMelonInfo(metadata, attribute, relativePath, index);

                    if (readInfo is not InstalledModInfo modInfo) continue;

                    result.Add(modInfo);
                    index++;
                }
                catch (BadImageFormatException) {}
            }
        }
        catch (BadImageFormatException) {}
        catch (IOException) {}
        catch (UnauthorizedAccessException) {}
    }

    private static InstalledModInfo? ReadMelonInfo(
        MetadataReader metadata, CustomAttribute attribute, string relativePath, int index)
    {
        BlobHandle signature;
        EntityHandle type;

        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                var member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                type = member.Parent;
                signature = member.Signature;
                break;

            case HandleKind.MethodDefinition:
                var method = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                type = method.GetDeclaringType();
                signature = method.Signature;
                break;

            default:
                return null;
        }


        var (name, @namespace) = type.Kind switch
        {
            HandleKind.TypeReference => ReadTypeReference(metadata.GetTypeReference((TypeReferenceHandle)type)),
            HandleKind.TypeDefinition => ReadTypeDefinition(metadata.GetTypeDefinition((TypeDefinitionHandle)type)),
            _ => default
        };
        
        if (name.IsNil || !metadata.StringComparer.Equals(name, "MelonInfoAttribute") || !metadata.StringComparer.Equals(@namespace, "MelonLoader"))
        {
            return null;
        }

        var parameters = metadata.GetBlobReader(signature);
        var header = parameters.ReadSignatureHeader();
        if (header.Kind != SignatureKind.Method || header.IsGeneric) return null;

        var count = parameters.ReadCompressedInteger();
        var reader = metadata.GetBlobReader(attribute.Value);
        if (reader.ReadUInt16() != 1 || reader.ReadSerializedString() == null) return null;


        var modName = reader.ReadSerializedString() ?? "UNKNOWN";
        string? version;

        switch (count)
        {
            case 4 or 5:
                version = reader.ReadSerializedString();
                break;

            case 7 or 8:
                var major = reader.ReadInt32();
                var minor = reader.ReadInt32();
                var revision = reader.ReadInt32();
                var identifier = count == 8 ? reader.ReadSerializedString() : null;
                version = string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{revision}{identifier}");
                break;

            default:
                return null;
        }

        return new InstalledModInfo
        {
            Id = $"external:{relativePath}:{index}",
            ExternalPath = relativePath,
            Name = modName,
            Author = reader.ReadSerializedString() ?? "UNKNOWN",
            Version = version ?? ""
        };

        static (StringHandle name, StringHandle @namespace) ReadTypeReference(TypeReference type) => (type.Name, type.Namespace);
        static (StringHandle name, StringHandle @namespace) ReadTypeDefinition(TypeDefinition type) => (type.Name, type.Namespace);
    }
}
