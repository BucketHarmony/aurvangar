using Aurvangar.Sim.Content;

namespace Aurvangar.Sim.Tests.Support;

/// <summary>Shared ContentDb (loading is not free; content is immutable).</summary>
public static class TestContent
{
    private static readonly Lazy<ContentDb> _db = new(ContentDb.LoadEmbedded);
    public static ContentDb Db => _db.Value;

    /// <summary>Repository root (directory containing CLAUDE.md).</summary>
    public static string RepoRoot
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("AURVANGAR_REPO_ROOT");
            if (!string.IsNullOrEmpty(env)) return env;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root (CLAUDE.md).");
        }
    }
}
