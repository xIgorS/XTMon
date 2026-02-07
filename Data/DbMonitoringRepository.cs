using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;
using XTMon.Models;
using XTMon.Options;

namespace XTMon.Data;

public sealed class DbMonitoringRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly MonitoringOptions _options;
    private readonly ReplayFlowsOptions _replayOptions;

    public DbMonitoringRepository(
        SqlConnectionFactory connectionFactory,
        IOptions<MonitoringOptions> options,
        IOptions<ReplayFlowsOptions> replayOptions)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _replayOptions = replayOptions.Value;
    }

    public async Task<MonitoringTableResult> GetDbSizePlusDiskAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = _options.DbSizePlusDiskStoredProcedure;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = _options.CommandTimeoutSeconds;

        await connection.OpenAsync(cancellationToken);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        var rows = new List<IReadOnlyList<string?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new string?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (reader.IsDBNull(i))
                {
                    row[i] = null;
                    continue;
                }

                var value = reader.GetValue(i);
                row[i] = value switch
                {
                    DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
                    DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss"),
                    decimal dec => dec.ToString("0.##"),
                    double dbl => dbl.ToString("0.##"),
                    float flt => flt.ToString("0.##"),
                    _ => value.ToString()
                };
            }

            rows.Add(row);
        }

        return new MonitoringTableResult(columns, rows);
    }

    public async Task<IReadOnlyList<FailedFlowRow>> GetFailedFlowsAsync(DateOnly pnlDate, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = _replayOptions.GetFailedFlowsStoredProcedure;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = _replayOptions.CommandTimeoutSeconds;

        var pnlDateParameter = new SqlParameter("@PnlDate", SqlDbType.Date)
        {
            Value = pnlDate.ToDateTime(TimeOnly.MinValue)
        };
        command.Parameters.Add(pnlDateParameter);

        await connection.OpenAsync(cancellationToken);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var flowIdIndex = reader.GetOrdinal("FlowId");
        var flowIdDerivedFromIndex = reader.GetOrdinal("FlowIdDerivedFrom");
        var businessDataTypeIdIndex = reader.GetOrdinal("BusinessDataTypeId");
        var feedSourceIdIndex = reader.GetOrdinal("FeedSourceId");
        var pnlDateIndex = reader.GetOrdinal("PnlDate");
        var reportingDateIndex = reader.GetOrdinal("ReportingDate");
        var fileNameIndex = reader.GetOrdinal("FileName");
        var arrivalDateIndex = reader.GetOrdinal("ArrivalDate");
        var packageGuidIndex = reader.GetOrdinal("PackageGuid");
        var currentStepIndex = reader.GetOrdinal("CurrentStep");
        var isFailedIndex = reader.GetOrdinal("IsFailed");
        var typeOfCalculationIndex = reader.GetOrdinal("TypeOfCalculation");
        var withBackdatedIndex = reader.GetOrdinal("WithBackdated");
        var skipCoreProcessIndex = reader.GetOrdinal("SkipCoreProcess");
        var droptabletpmIndex = reader.GetOrdinal("Droptabletpm");

        var rows = new List<FailedFlowRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var flowId = reader.IsDBNull(flowIdIndex) ? (long?)null : reader.GetInt64(flowIdIndex);
            var flowIdDerivedFrom = reader.IsDBNull(flowIdDerivedFromIndex) ? (long?)null : reader.GetInt64(flowIdDerivedFromIndex);
            var businessDataTypeId = reader.IsDBNull(businessDataTypeIdIndex) ? (short?)null : reader.GetInt16(businessDataTypeIdIndex);
            var feedSourceId = reader.IsDBNull(feedSourceIdIndex) ? (short?)null : reader.GetInt16(feedSourceIdIndex);
            var pnlDateValue = DateOnly.FromDateTime(reader.GetDateTime(pnlDateIndex));
            var reportingDate = reader.IsDBNull(reportingDateIndex)
                ? (DateOnly?)null
                : DateOnly.FromDateTime(reader.GetDateTime(reportingDateIndex));
            var fileName = reader.IsDBNull(fileNameIndex) ? null : reader.GetString(fileNameIndex);
            var arrivalDate = reader.IsDBNull(arrivalDateIndex) ? (DateTime?)null : reader.GetDateTime(arrivalDateIndex);
            var packageGuid = reader.IsDBNull(packageGuidIndex) ? (Guid?)null : reader.GetGuid(packageGuidIndex);
            var currentStep = reader.IsDBNull(currentStepIndex) ? null : reader.GetString(currentStepIndex);
            var isFailed = reader.IsDBNull(isFailedIndex) ? null : reader.GetString(isFailedIndex);
            var typeOfCalculation = reader.IsDBNull(typeOfCalculationIndex) ? null : reader.GetString(typeOfCalculationIndex);
            var withBackdated = ReadBoolean(reader, withBackdatedIndex);
            var skipCoreProcess = ReadBoolean(reader, skipCoreProcessIndex);
            var droptabletpm = ReadBoolean(reader, droptabletpmIndex);

            rows.Add(new FailedFlowRow(
                flowId,
                flowIdDerivedFrom,
                businessDataTypeId,
                feedSourceId,
                pnlDateValue,
                reportingDate,
                fileName,
                arrivalDate,
                packageGuid,
                currentStep,
                isFailed,
                typeOfCalculation,
                withBackdated,
                skipCoreProcess,
                droptabletpm));
        }

        return rows;
    }

    private static bool ReadBoolean(IDataRecord reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return false;
        }

        var value = reader.GetValue(ordinal);
        if (value is bool boolValue)
        {
            return boolValue;
        }

        return Convert.ToInt32(value) != 0;
    }

    public async Task ReplayFlowsAsync(IReadOnlyCollection<ReplayFlowSubmissionRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = _replayOptions.ReplayFlowsStoredProcedure;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = _replayOptions.CommandTimeoutSeconds;

        var table = new DataTable();
        table.Columns.Add("FlowIdDerivedFrom", typeof(long));
        table.Columns.Add("FlowId", typeof(long));
        table.Columns.Add("PnlDate", typeof(DateTime));
        table.Columns.Add("WithBackdated", typeof(bool));
        table.Columns.Add("SkipCoreProcess", typeof(bool));
        table.Columns.Add("Droptabletpm", typeof(bool));

        foreach (var row in rows)
        {
            table.Rows.Add(
                row.FlowIdDerivedFrom,
                row.FlowId,
                row.PnlDate.ToDateTime(TimeOnly.MinValue),
                row.WithBackdated,
                row.SkipCoreProcess,
                row.Droptabletpm);
        }

        var parameter = new SqlParameter("@FlowData", SqlDbType.Structured)
        {
            TypeName = "administration.ReplayAdjAtCoreSet",
            Value = table
        };
        command.Parameters.Add(parameter);

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
