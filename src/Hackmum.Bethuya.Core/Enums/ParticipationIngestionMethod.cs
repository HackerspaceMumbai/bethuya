namespace Hackmum.Bethuya.Core.Enums;

/// <summary>
/// Distinguishes how a participation ledger entry entered the system, independent of which
/// platform (<see cref="ParticipationConnectorKind"/>) it originated from.
/// </summary>
public enum ParticipationIngestionMethod
{
    /// <summary>Written by a live API/webhook connector.</summary>
    ApiOrWebhook,
    /// <summary>Written by the Registration &amp; Attendance Import feature from an uploaded file.</summary>
    FileImport
}
