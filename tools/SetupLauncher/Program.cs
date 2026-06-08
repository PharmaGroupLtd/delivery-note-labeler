using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

ApplicationConfiguration.Initialize();

var extractDir = Path.Combine(
    Path.GetTempPath(),
    "DeliveryNoteLabeler-Setup-" + Guid.NewGuid().ToString("N"));

try
{
    Directory.CreateDirectory(extractDir);

    var zipPath = Path.Combine(extractDir, "DeliveryNoteLabeler-package.zip");
    await ExtractEmbeddedPackageAsync(zipPath).ConfigureAwait(false);
    ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

    var packageRoot = ResolvePackageRoot(extractDir);
    var installScript = Path.Combine(packageRoot, "Install.ps1");
    if (!File.Exists(installScript))
    {
        throw new FileNotFoundException("Install.ps1 was not found inside the package.", installScript);
    }

    var exitCode = RunPowerShell(installScript);
    if (exitCode != 0)
    {
        ShowError("Installation failed. Contact IT support if this keeps happening.");
        return 1;
    }

    var installedExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "Delivery Note Labeler",
        "DeliveryNoteLabeler.exe");

    if (!File.Exists(installedExe))
    {
        ShowError($"Installation did not create the app at:{Environment.NewLine}{installedExe}");
        return 1;
    }

    var launch = MessageBox.Show(
        "Delivery Note Labeler was installed successfully.\n\nOpen the app now?",
        "Delivery Note Labeler",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Information);

    if (launch == DialogResult.Yes)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installedExe,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(installedExe)!,
        });
    }

    return 0;
}
catch (Exception ex)
{
    ShowError(ex.Message);
    return 1;
}
finally
{
    try
    {
        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, recursive: true);
        }
    }
    catch
    {
        // Best-effort cleanup only.
    }
}

static void ShowError(string message)
{
    MessageBox.Show(
        message,
        "Delivery Note Labeler Setup",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
}

static string ResolvePackageRoot(string extractDir)
{
    var directInstallScript = Path.Combine(extractDir, "Install.ps1");
    if (File.Exists(directInstallScript))
    {
        return extractDir;
    }

    var nestedDirectories = Directory.GetDirectories(extractDir);
    if (nestedDirectories.Length == 1)
    {
        var nestedRoot = nestedDirectories[0];
        if (File.Exists(Path.Combine(nestedRoot, "Install.ps1")))
        {
            return nestedRoot;
        }
    }

    throw new InvalidOperationException(
        "The installer package is invalid. Download a fresh copy of DeliveryNoteLabeler-Setup.exe and try again.");
}

static async Task ExtractEmbeddedPackageAsync(string destinationPath)
{
    var assembly = Assembly.GetExecutingAssembly();
    var resourceName = assembly.GetManifestResourceNames()
        .SingleOrDefault(name => name.EndsWith("DeliveryNoteLabeler-package.zip", StringComparison.Ordinal))
        ?? throw new InvalidOperationException("The installer package is missing from this executable.");

    await using var resourceStream = assembly.GetManifestResourceStream(resourceName);
    if (resourceStream is null)
    {
        throw new InvalidOperationException("The installer package is missing from this executable.");
    }

    await using var destination = File.Create(destinationPath);
    await resourceStream.CopyToAsync(destination).ConfigureAwait(false);
}

static int RunPowerShell(string scriptPath)
{
    var powershell = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "System32",
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    if (!File.Exists(powershell))
    {
        powershell = "powershell.exe";
    }

    var startInfo = new ProcessStartInfo
    {
        FileName = powershell,
        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    using var process = Process.Start(startInfo);
    if (process is null)
    {
        throw new InvalidOperationException("Could not start PowerShell.");
    }

    process.WaitForExit();
    return process.ExitCode;
}
