// src/ChurchProjection.Application/Live/LiveCommand.cs
namespace ChurchProjection.Application.Live;

/// <summary>
/// One operator action. The same object arrives over the hub and over HTTP —
/// the control view uses the socket, the tests and a recovering client use the
/// endpoint, and neither gets a different set of rules.
/// </summary>
public sealed record LiveCommand(string? Type, string? ItemId, int? PageIndex, bool? On);
