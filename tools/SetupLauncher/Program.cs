using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

ApplicationConfiguration.Initialize();

var logPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Delivery Note Labeler",
    "install.log");

var extractDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Delivery Note Labeler",
    "SetupExtract");

try
{
    Log(logPath, "Starting Delivery Note Labeler setup.");

    if (Directory.Exists(extractDir))
    {
        TryDeleteDirectory(extractDir);
    }

    Directory.CreateDirectory(extractDir);

    var zipPath = Path.Combine(extractDir, "DeliveryNoteLabeler-package.zip");
    await ExtractEmbeddedPackageAsync(zipPath).ConfigureAwait(false);
    ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

    var packageRoot = ResolvePackageRoot(extractDir);
    Log(logPath, $"Package root: {packageRoot}");

    InstallFromPackage(packageRoot, logPath);

    var installedExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "Delivery Note Labeler",
        "DeliveryNoteLabeler.exe");

    if (!File.Exists(installedExe))
    {
        throw new FileNotFoundException(
            "Installation did not create the app executable.",
            installedExe);
    }

    Log(logPath, "Installation completed successfully.");

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
    Log(logPath, $"Installation failed: {ex}");
    ShowError(
        "Installation failed.\n\n"
        + ex.Message
        + "\n\nDetails were saved to:\n"
        + logPath
        + "\n\nIf this PC is managed by your company, try the zip install instead:\n"
        + "1. Download DeliveryNoteLabeler-*-win-x64.zip\n"
        + "2. Extract it\n"
        + "3. Double-click Install.cmd");
    return 1;
}
finally
{
    TryDeleteDirectory(extractDir);
}

static void InstallFromPackage(string packageRoot, string logPath)
{
    var installDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "Delivery Note Labeler");

    var sourceExe = Path.Combine(packageRoot, "DeliveryNoteLabeler.exe");
    if (!File.Exists(sourceExe))
    {
        throw new FileNotFoundException(
            "DeliveryNoteLabeler.exe was not found in the installer package.",
            sourceExe);
    }

    Log(logPath, $"Installing from {packageRoot} to {installDir}");

    StopRunningApp(logPath);

    if (Directory.Exists(installDir))
    {
        Log(logPath, "Removing previous install folder.");
        TryDeleteDirectory(installDir);
    }

    Directory.CreateDirectory(installDir);

    var skipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Install.ps1",
        "Install.cmd",
        "Uninstall.ps1",
        "README.txt",
        "DeliveryNoteLabeler-package.zip",
    };

    foreach (var entry in Directory.EnumerateFileSystemEntries(packageRoot))
    {
        var name = Path.GetFileName(entry);
        if (skipNames.Contains(name))
        {
            continue;
        }

        var destination = Path.Combine(installDir, name);
        if (Directory.Exists(entry))
        {
            CopyDirectory(entry, destination);
        }
        else
        {
            File.Copy(entry, destination, overwrite: true);
        }
    }

    var installedExe = Path.Combine(installDir, "DeliveryNoteLabeler.exe");
    var launchCmdPath = Path.Combine(installDir, "PrintLabels.cmd");
    var launchPs1Path = Path.Combine(installDir, "PrintLabels.ps1");

    if (!File.Exists(installedExe))
    {
        throw new FileNotFoundException("Installation failed: app executable was not copied.", installedExe);
    }

    if (!File.Exists(launchCmdPath) || !File.Exists(launchPs1Path))
    {
        throw new FileNotFoundException("Installation failed: Print Labels launcher files were not copied.");
    }

    RegisterPrintLabelsContextMenu(launchCmdPath, Path.Combine(installDir, "DeliveryNoteLabeler.ico"), installedExe, logPath);
}

static void RegisterPrintLabelsContextMenu(string launchCmdPath, string iconPath, string exePath, string logPath)
{
    var iconValue = File.Exists(iconPath) ? iconPath : $"{exePath},0";
    var command = $"\"{launchCmdPath}\" %*";

    var registryPaths = new[]
    {
        @"Software\Classes\SystemFileAssociations\.pdf\shell\PrintLabels",
        @"Software\Classes\.pdf\shell\PrintLabels",
    };

    foreach (var registryPath in registryPaths)
    {
        using var shellKey = Registry.CurrentUser.CreateSubKey(registryPath, writable: true)
            ?? throw new InvalidOperationException($"Could not create registry key: HKCU\\{registryPath}");

        shellKey.SetValue(null, "Print Labels");
        shellKey.SetValue("Icon", iconValue);
        shellKey.SetValue("MultiSelectModel", "Document");

        using var commandKey = shellKey.CreateSubKey("command", writable: true)
            ?? throw new InvalidOperationException($"Could not create registry key: HKCU\\{registryPath}\\command");

        commandKey.SetValue(null, command);
    }

    Log(logPath, "Registered Print Labels context menu.");
}

static void StopRunningApp(string logPath)
{
    foreach (var process in Process.GetProcessesByName("DeliveryNoteLabeler"))
    {
        try
        {
            Log(logPath, $"Stopping running app process {process.Id}.");
            process.CloseMainWindow();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Log(logPath, $"Could not stop process {process.Id}: {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }
}

static void CopyDirectory(string sourceDir, string destinationDir)
{
    Directory.CreateDirectory(destinationDir);

    foreach (var entry in Directory.EnumerateFileSystemEntries(sourceDir))
    {
        var name = Path.GetFileName(entry);
        var destination = Path.Combine(destinationDir, name);
        if (Directory.Exists(entry))
        {
            CopyDirectory(entry, destination);
        }
        else
        {
            File.Copy(entry, destination, overwrite: true);
        }
    }
}

static void TryDeleteDirectory(string path)
{
    if (!Directory.Exists(path))
    {
        return;
    }

    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
    {
        try
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        catch
        {
            // Best effort.
        }
    }

    Directory.Delete(path, recursive: true);
}

static void Log(string logPath, string message)
{
    try
    {
        var logDir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        File.AppendAllText(
            logPath,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}",
            Encoding.UTF8);
    }
    catch
    {
        // Ignore logging failures.
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
        if (File.Exists(Path.Combine(nestedRoot, "Install.ps1"))
            || File.Exists(Path.Combine(nestedRoot, "DeliveryNoteLabeler.exe")))
        {
            return nestedRoot;
        }
    }

    if (File.Exists(Path.Combine(extractDir, "DeliveryNoteLabeler.exe")))
    {
        return extractDir;
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
