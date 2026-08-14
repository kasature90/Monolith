using Robust.Shared.Configuration;

namespace Content.Shared._SS220.CCVars;

[CVarDefs]
public sealed class CCVars220
{
    /// <summary>
    /// Whether to rotate doors when map is loaded
    /// </summary>
    public static readonly CVarDef<bool> MigrationAlignDoors =
        CVarDef.Create("map_migration.align_doors", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
