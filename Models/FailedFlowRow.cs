namespace XTMon.Models;

public sealed record FailedFlowRow(
    long? FlowId,
    long? FlowIdDerivedFrom,
    short? BusinessDataTypeId,
    short? FeedSourceId,
    string? FeedSourceName,
    DateOnly PnlDate,
    DateOnly? ReportingDate,
    string? FileName,
    DateTime? ArrivalDate,
    Guid? PackageGuid,
    string? CurrentStep,
    string? IsFailed,
    string? TypeOfCalculation,
    bool WithBackdated,
    bool SkipCoreProcess,
    bool Droptabletpm);
