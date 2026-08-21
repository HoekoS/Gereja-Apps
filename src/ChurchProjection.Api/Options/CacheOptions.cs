namespace ChurchProjection.Api.Options;

public sealed class CacheOptions
{
    public const string Section = "Cache";

    public RedisOptions Redis { get; set; } = new();

    public sealed class RedisOptions
    {
        /// <summary>Null or empty selects the in-process cache (NFR-REL-09).</summary>
        public string? ConnectionString { get; set; }
    }
}
