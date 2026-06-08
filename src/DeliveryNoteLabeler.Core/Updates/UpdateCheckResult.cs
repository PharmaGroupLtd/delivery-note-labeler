namespace DeliveryNoteLabeler.Core.Updates;

public sealed class UpdateCheckResult
{
    public static UpdateCheckResult NotConfigured { get; } = new(UpdateCheckStatus.NotConfigured);

    public static UpdateCheckResult UpToDate { get; } = new(UpdateCheckStatus.UpToDate);

    public static UpdateCheckResult InvalidManifest { get; } = new(UpdateCheckStatus.InvalidManifest);

    private UpdateCheckResult(UpdateCheckStatus status, UpdateManifest? manifest = null, string? errorMessage = null)
    {
        Status = status;
        Manifest = manifest;
        ErrorMessage = errorMessage;
    }

    public UpdateCheckStatus Status { get; }

    public UpdateManifest? Manifest { get; }

    public string? ErrorMessage { get; }

    public bool IsUpdateAvailable => Status == UpdateCheckStatus.UpdateAvailable;

    public static UpdateCheckResult UpdateAvailable(UpdateManifest manifest) =>
        new(UpdateCheckStatus.UpdateAvailable, manifest);

    public static UpdateCheckResult Failed(string message) =>
        new(UpdateCheckStatus.Failed, errorMessage: message);
}

public enum UpdateCheckStatus
{
    NotConfigured,
    UpToDate,
    UpdateAvailable,
    InvalidManifest,
    Failed,
}
