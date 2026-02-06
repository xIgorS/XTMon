using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using System.Linq;
using XTMon.Data;
using XTMon.Models;
using XTMon.Options;

namespace XTMon.Components.Pages;

public partial class Monitoring : ComponentBase
{
    private const string DatabaseNameColumn = "DatabaseName";

    [Inject]
    private DbMonitoringRepository Repository { get; set; } = default!;

    [Inject]
    private IOptions<MonitoringOptions> MonitoringOptions { get; set; } = default!;

    private MonitoringTableResult? result;
    private IReadOnlyList<DbCard> dbCards = Array.Empty<DbCard>();
    private bool isLoading;
    private string? loadError;
    private DateTimeOffset? lastRefresh;

    private string ProcedureName => MonitoringOptions.Value.DbSizePlusDiskStoredProcedure;

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        isLoading = true;
        loadError = null;

        try
        {
            result = await Repository.GetDbSizePlusDiskAsync(CancellationToken.None);
            BuildDbCards();
            lastRefresh = DateTimeOffset.Now;
        }
        catch (Exception ex)
        {
            loadError = ex.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

    private void BuildDbCards()
    {
        if (result is null || result.Rows.Count == 0)
        {
            dbCards = Array.Empty<DbCard>();
            return;
        }

        var dbNameIndex = FindColumnIndex(DatabaseNameColumn);
        if (dbNameIndex < 0)
        {
            dbCards = Array.Empty<DbCard>();
            return;
        }

        var tableColumns = result.Columns
            .Select((label, index) => new { label, index })
            .Where(item => item.index != dbNameIndex)
            .ToList();

        dbCards = result.Rows
            .Where(row => row.Count > dbNameIndex)
            .GroupBy(row => row[dbNameIndex] ?? "Unknown")
            .Select(group =>
            {
                var rows = group
                    .Select(row =>
                        (IReadOnlyList<string?>)tableColumns
                            .Select(item => row.Count > item.index ? row[item.index] : null)
                            .ToList())
                    .ToList();

                return new DbCard(group.Key, tableColumns.Select(item => item.label).ToList(), rows);
            })
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int FindColumnIndex(string columnName)
    {
        if (result is null)
        {
            return -1;
        }

        for (var i = 0; i < result.Columns.Count; i++)
        {
            if (string.Equals(result.Columns[i], columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private sealed record DbCard(
        string Name,
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<string?>> Rows);
}
