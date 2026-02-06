using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Linq;
using System.Text;
using XTMon.Data;
using XTMon.Models;
using XTMon.Options;

namespace XTMon.Components.Pages;

public partial class Monitoring : ComponentBase
{
    private const string DatabaseNameColumn = "DatabaseName";
    private static readonly string[] CardColumnOrder =
    [
        "FileGroup",
        "AllocatedSpaceMB",
        "UsedSpaceMB",
        "FreeSpaceMB",
        "Autogrow",
        "FreeDriveMB",
        "PartSizeMB",
        "TotalFreeSpaceMB",
        "AlertLevel"
    ];

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

        var tableColumns = CardColumnOrder
            .Select(column => new { column, index = FindColumnIndex(column) })
            .Where(item => item.index >= 0)
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

                return new DbCard(group.Key, tableColumns.Select(item => item.column).ToList(), rows);
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

    private static string ToHeaderLabel(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(columnName.Length + 4);
        for (var i = 0; i < columnName.Length; i++)
        {
            var current = columnName[i];
            if (i > 0)
            {
                var previous = columnName[i - 1];
                var next = i + 1 < columnName.Length ? columnName[i + 1] : '\0';
                var boundary = char.IsUpper(current) &&
                               (char.IsLower(previous) || (char.IsUpper(previous) && char.IsLower(next)));

                if (boundary)
                {
                    builder.Append(' ');
                }
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static string FormatCellValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return FormatWithSpaces(number);
        }

        return value;
    }

    private static string FormatWithSpaces(long value)
    {
        var negative = value < 0;
        var digits = Math.Abs(value).ToString(CultureInfo.InvariantCulture);
        var builder = new StringBuilder(digits.Length + digits.Length / 3 + 1);

        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                builder.Append(' ');
            }

            builder.Append(digits[i]);
        }

        return negative ? "-" + builder : builder.ToString();
    }

    private sealed record DbCard(
        string Name,
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<string?>> Rows);
}
