using Hackmum.Bethuya.Core.Enums;

namespace Hackmum.Bethuya.Core.Services;

/// <summary>
/// Maps a file-import source platform to the canonical participation connector used across the
/// unified ledger, so imported attendance entries project alongside live-connector signals.
/// </summary>
public static class ImportConnectorMapper
{
    public static ParticipationConnectorKind ToConnector(ImportSourceKind sourceKind) => sourceKind switch
    {
        ImportSourceKind.Luma => ParticipationConnectorKind.Luma,
        ImportSourceKind.MLH => ParticipationConnectorKind.MLH,
        ImportSourceKind.Custom => ParticipationConnectorKind.Custom,
        _ => ParticipationConnectorKind.Custom
    };
}
