using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;
using System.Text;
using XTMon.Data;
using XTMon.Models;

namespace XTMon.Components.Pages;

public partial class ReplayFlows : ComponentBase
{
    private const string DisplayDateFormat = "dd-MM-yyyy";
    private static readonly string[] AcceptedDateFormats =
    [
        "dd-MM-yyyy",
        "yyyy-MM-dd",
        "dd/MM/yyyy",
        "yyyy/MM/dd"
    ];

    [Inject]
    private DbMonitoringRepository Repository { get; set; } = default!;

    private readonly List<ReplayFlowGridRow> rows = new();
    private string pnlDateInput = string.Empty;
    private string? pnlDateError;
    private bool isLoading;
    private bool isSubmitting;
    private bool hasLoaded;
    private string? loadError;
    private string? statusMessage;
    private bool statusIsError;
    private DateTimeOffset? lastRefresh;
    private DateOnly? lastPnlDate;

    private string PnlDateInput
    {
        get => pnlDateInput;
        set
        {
            pnlDateInput = value;
            if (!string.IsNullOrWhiteSpace(pnlDateError))
            {
                pnlDateError = null;
            }
        }
    }

    private int SelectedRowsCount => rows.Count(row => row.IsSelected);

    private bool CanSubmit => !isSubmitting && !isLoading && SelectedRowsCount > 0;

    private async Task ReloadAsync()
    {
        if (!TryGetPnlDate(out var pnlDate))
        {
            return;
        }

        isLoading = true;
        loadError = null;
        statusMessage = null;

        try
        {
            var data = await Repository.GetFailedFlowsAsync(pnlDate, CancellationToken.None);
            rows.Clear();
            rows.AddRange(data.Select(row => new ReplayFlowGridRow(row)));
            hasLoaded = true;
            lastRefresh = DateTimeOffset.Now;
            lastPnlDate = pnlDate;
        }
        catch (Exception ex)
        {
            loadError = ex.Message;
            rows.Clear();
            hasLoaded = false;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task SubmitAsync()
    {
        if (!hasLoaded)
        {
            SetStatus("Load data first by entering a Pnl date and pressing Enter.", isError: true);
            return;
        }

        var selectedRows = rows.Where(row => row.IsSelected).ToList();
        if (selectedRows.Count == 0)
        {
            SetStatus("Select at least one row to submit.", isError: true);
            return;
        }

        var submissionRows = new List<ReplayFlowSubmissionRow>(selectedRows.Count);
        foreach (var row in selectedRows)
        {
            if (!row.Source.FlowId.HasValue || !row.Source.FlowIdDerivedFrom.HasValue)
            {
                SetStatus("Selected rows must have FlowId and FlowIdDerivedFrom values.", isError: true);
                return;
            }

            submissionRows.Add(new ReplayFlowSubmissionRow(
                row.Source.FlowIdDerivedFrom.Value,
                row.Source.FlowId.Value,
                row.Source.PnlDate,
                row.Source.WithBackdated,
                row.SkipCoreProcess,
                row.Droptabletpm));
        }

        isSubmitting = true;
        SetStatus(null, isError: false);

        try
        {
            await Repository.ReplayFlowsAsync(submissionRows, CancellationToken.None);
            SetStatus($"Submitted {submissionRows.Count} row(s) for replay.", isError: false);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            isSubmitting = false;
        }
    }

    private bool TryGetPnlDate(out DateOnly pnlDate)
    {
        if (string.IsNullOrWhiteSpace(pnlDateInput))
        {
            pnlDateError = "Enter a Pnl date in DD-MM-YYYY format.";
            pnlDate = default;
            return false;
        }

        if (!DateOnly.TryParseExact(pnlDateInput, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out pnlDate))
        {
            pnlDateError = "Enter a Pnl date in DD-MM-YYYY format.";
            return false;
        }

        pnlDateInput = pnlDate.ToString(DisplayDateFormat, CultureInfo.InvariantCulture);

        pnlDateError = null;
        return true;
    }

    private bool IsPnlDateValid()
    {
        return DateOnly.TryParseExact(pnlDateInput, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private async Task OnPnlDateKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await ReloadAsync();
        }
    }

    private void NormalizePnlDateInput()
    {
        if (DateOnly.TryParseExact(pnlDateInput, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var pnlDate))
        {
            pnlDateInput = pnlDate.ToString(DisplayDateFormat, CultureInfo.InvariantCulture);
        }
    }

    private void SetStatus(string? message, bool isError)
    {
        statusMessage = message;
        statusIsError = isError;
    }

    private static string FormatText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string FormatGuid(Guid? value)
    {
        return value?.ToString() ?? "-";
    }

    private static string FormatBool(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString(DisplayDateFormat, CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateOnly? value)
    {
        return value is null ? "-" : value.Value.ToString(DisplayDateFormat, CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateTime? value)
    {
        if (value is null)
        {
            return "-";
        }

        return DateOnly.FromDateTime(value.Value).ToString(DisplayDateFormat, CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(long? value)
    {
        return value.HasValue ? FormatWithSpaces(value.Value) : "-";
    }

    private static string FormatNumber(short? value)
    {
        return value.HasValue ? FormatWithSpaces(value.Value) : "-";
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

    private sealed class ReplayFlowGridRow
    {
        public ReplayFlowGridRow(FailedFlowRow source)
        {
            Source = source;
            SkipCoreProcess = source.SkipCoreProcess;
            Droptabletpm = source.Droptabletpm;
        }

        public FailedFlowRow Source { get; }

        public bool IsSelected { get; set; }

        public bool SkipCoreProcess { get; set; }

        public bool Droptabletpm { get; set; }
    }
}
