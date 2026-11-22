using System;
using System.Collections.Generic;

namespace StellarModManager.Models;

public sealed class GameModProfiles
{
    public Guid ActiveProfileId { get; set; }
    public Guid? LastOperationId { get; set; }
    public List<ModProfile> Profiles { get; set; } = [];
    public List<ProfileMod> InstalledMods { get; set; } = [];
}

public sealed record ProfileMod(InstalledModInfo Mod, List<string> Files);
