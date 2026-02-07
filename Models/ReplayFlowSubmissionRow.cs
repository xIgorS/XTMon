namespace XTMon.Models;

public sealed record ReplayFlowSubmissionRow(
    long FlowIdDerivedFrom,
    long FlowId,
    DateOnly PnlDate,
    bool WithBackdated,
    bool SkipCoreProcess,
    bool Droptabletpm);
