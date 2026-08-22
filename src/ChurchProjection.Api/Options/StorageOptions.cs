namespace ChurchProjection.Api.Options;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Absolute or relative path to the SQLite file. Created on start.</summary>
    public string DatabasePath { get; set; } = "data/projection.db";

    /// <summary>The only directory media is ever read from or written to.</summary>
    public string MediaRoot { get; set; } = "data/media";

    /// <summary>
    /// Rebases the relative defaults on the content root. Everything downstream
    /// resolves a relative path against the working directory, which for a
    /// service started by sc.exe is C:\Windows\System32 — so the values are made
    /// absolute once, here, rather than trusted to be absolute in configuration.
    /// </summary>
    public void MakeAbsolute(string contentRoot)
    {
        DatabasePath = Path.GetFullPath(DatabasePath, contentRoot);
        MediaRoot = Path.GetFullPath(MediaRoot, contentRoot);
    }
}
