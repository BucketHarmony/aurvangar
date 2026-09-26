using Colony.Sim.Content;

namespace Colony.Sim.Save;

/// <summary>Binary save/load of the full sim state (docs/specs/save-load.md). M4-T10.</summary>
public static class SaveGame
{
    public const string Magic = "CSAV";
    public const int FormatVersion = 1;

    public static void Save(Simulation sim, Stream output) =>
        throw new NotImplementedException("M4-T10: SaveGame.Save (SAV-01)");

    public static Simulation Load(Stream input, ContentDb content) =>
        throw new NotImplementedException("M4-T10: SaveGame.Load (SAV-02)");
}
