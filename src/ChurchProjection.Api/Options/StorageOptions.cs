namespace ChurchProjection.Api.Options;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Absolute or relative path to the SQLite file. Created on start.</summary>
    public string DatabasePath { get; set; } = "data/projection.db";

    /// <summary>The only directory media is ever read from or written to.</summary>
    public string MediaRoot { get; set; } = "data/media";
}
