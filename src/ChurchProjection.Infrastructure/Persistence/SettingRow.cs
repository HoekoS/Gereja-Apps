namespace ChurchProjection.Infrastructure.Persistence;

public sealed class SettingRow
{
    public required string Key { get; init; }

    public required string Value { get; set; }
}
