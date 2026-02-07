namespace XTMon.Options;

public sealed class ReplayFlowsOptions
{
    public const string SectionName = "ReplayFlows";

    public string GetFailedFlowsStoredProcedure { get; set; } = "administration.GetFailedFlows";
    public string ReplayFlowsStoredProcedure { get; set; } = "administration.UspReplayFlows";
    public int CommandTimeoutSeconds { get; set; } = 30;
}
