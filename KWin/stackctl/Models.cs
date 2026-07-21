using System.Text.Json.Serialization;

namespace StackCtl;

internal sealed record LayoutFile(string Id, string? Name, List<ZoneFile>? Zones)
{
    public string Name { get; } = string.IsNullOrWhiteSpace(Name) ? Id : Name!;
    public List<ZoneFile> Zones { get; } = Zones ?? new();
}

internal sealed record ZoneFile(string Id, string? Name, string? Mode, bool IsDropZone, string? TargetZoneId)
{
    public string Mode { get; } = string.IsNullOrWhiteSpace(Mode) ? "stack" : Mode!;
}

internal sealed record ScreenKey(string Name, string Key, string Geometry);

internal sealed record AutoCaptureIgnoreFilter(
    [property: JsonPropertyName("title")] AutoCaptureClause? Title,
    [property: JsonPropertyName("class")] AutoCaptureClause? Class,
    [property: JsonPropertyName("app")] AutoCaptureClause? App
);

internal sealed record AutoCaptureClause(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("match")] string Match
);
