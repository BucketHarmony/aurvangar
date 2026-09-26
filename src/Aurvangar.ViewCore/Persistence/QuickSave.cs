using Aurvangar.Sim;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Save;

namespace Aurvangar.ViewCore.Persistence;

/// <summary>F5 quick save / F9 quick load (VIEW-19) to one file. Called between ticks. Failures come back as a short
/// player-facing message instead of an exception. A save is written to a temporary file first and then moved over
/// the old one, so a failed save never destroys the previous quick save.</summary>
public static class QuickSave
{
    public const string NoSaveMessage = "No quick save yet.";

    /// <summary>Returns null on success, else the error message.</summary>
    public static string? Save(Simulation sim, string path)
    {
        string tmp = path + ".tmp";
        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var f = File.Create(tmp)) SaveGame.Save(sim, f);
            File.Move(tmp, path, overwrite: true);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDelete(tmp);
            return "Save failed: " + e.Message;
        }
    }

    /// <summary>The loaded simulation, or null and the error message.</summary>
    public static (Simulation? Sim, string? Error) Load(string path, ContentDb content)
    {
        if (!File.Exists(path)) return (null, NoSaveMessage);
        try
        {
            using var f = File.OpenRead(path);
            return (SaveGame.Load(f, content), null);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return (null, "Load failed: " + e.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
