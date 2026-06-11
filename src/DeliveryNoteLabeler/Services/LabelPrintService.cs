using DeliveryNoteLabeler.Core.Configuration;
using DeliveryNoteLabeler.Core.Models;
using DeliveryNoteLabeler.Core.Printing;

namespace DeliveryNoteLabeler.Services;

/// <summary>
/// Sends label jobs to a configured Zebra printer as RAW ZPL.
/// </summary>
public sealed class LabelPrintService
{
    private static readonly TimeSpan InterLabelDelay = TimeSpan.FromMilliseconds(50);

    public Task PrintAsync(DeliveryNote note, IReadOnlyList<LabelJob> jobs, CancellationToken cancellationToken = default)
    {
        return PrintJobsAsync(jobs, cancellationToken);
    }

    public Task PrintSampleAsync(CancellationToken cancellationToken = default)
    {
        var sample = ZplGenerator.CreateSampleLabelJob();
        return PrintJobsAsync([sample], cancellationToken);
    }

    private static async Task PrintJobsAsync(IReadOnlyList<LabelJob> jobs, CancellationToken cancellationToken)
    {
        var printerName = AppConfig.GetPrinterName();
        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new InvalidOperationException("No printer is configured. Open Settings and select a printer.");
        }

        if (jobs.Count == 0)
        {
            return;
        }

        if (!LabelPrintValidator.TryValidatePartNumbers(jobs, out var validationError))
        {
            throw new InvalidOperationException(validationError);
        }

        var layout = AppConfig.GetLabelLayoutOptions();
        var logo = LabelBrandingProvider.GetLogo(layout);
        var printCount = 0;
        var totalLabels = LabelJob.CountLabelsToPrint(jobs);

        foreach (var job in jobs)
        {
            var zpl = ZplGenerator.BuildLabelZpl(job, layout, logo);
            for (var copy = 0; copy < job.LabelQuantity; copy++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RawPrinterHelper.SendRaw(printerName, zpl);

                printCount++;
                if (printCount < totalLabels)
                {
                    await Task.Delay(InterLabelDelay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
