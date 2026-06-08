# Delivery Note Labeler

Desktop app for parsing fixed-layout Pharma Sheet Metal delivery note PDFs, previewing expanded label jobs, and exporting them to CSV.

Built with **.NET 8 WPF** (native Windows UI). Phase 1 covers scanning, review queue, and CSV export. Phase 2 adds **Zebra GK420d printing** over a shared Windows printer queue.

## Requirements

- Windows 10/11 (64-bit)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — only if you build with `.\scripts\build.ps1` without `-SelfContained`
- Release packages from `.\scripts\package.ps1` include the runtime and do **not** require .NET on target PCs

To build the **Windows 11 primary** context menu (not just “Show more options”):

- Visual Studio Build Tools with the **Desktop development with C++** workload (builds `DeliveryNoteLabelerShell.dll`)
- **Windows 10/11 SDK** (provides `makeappx.exe` and `signtool.exe` for the sparse identity package)

## Run from source

```powershell
cd C:\Users\brook\delivery-note-labeler
dotnet run --project src/DeliveryNoteLabeler/DeliveryNoteLabeler.csproj
```

Open one or more PDFs from the command line (same as the Explorer context menu):

```powershell
dotnet run --project src/DeliveryNoteLabeler/DeliveryNoteLabeler.csproj -- "C:\path\to\note1.pdf" "C:\path\to\note2.pdf"
```

## Windows executable and Explorer menu

Build a published app and add **Print Labels** to the File Explorer right-click menu for PDF files.

**Print Labels** opens the app, scans the PDFs, and lets you print labels to a configured Zebra GK420d.

### Build

```powershell
.\scripts\build.ps1
```

This runs `dotnet publish` and outputs:

`dist\publish\DeliveryNoteLabeler.exe`

When the C++ build tools and Windows SDK are installed, the build also produces:

- `dist\publish\DeliveryNoteLabelerShell.dll` — Windows 11 context menu handler
- `dist\publish\DeliveryNoteLabeler.Sparse.msix` — sparse identity package for the Win11 menu

Framework-dependent publish keeps the install smaller when the .NET 8 Desktop Runtime is deployed once to all PCs (recommended for business deployment via Intune or MSI).

Framework-dependent publish (`.\scripts\build.ps1` without `-SelfContained`) keeps the install smaller when the .NET 8 Desktop Runtime is deployed once to all PCs (recommended for business deployment via Intune or MSI).

Self-contained publish (`.\scripts\build.ps1 -SelfContained` or `.\scripts\package.ps1`) bundles the runtime so other PCs do not need .NET installed.

### Install on this PC (developer / build machine)

```powershell
.\scripts\install.ps1
```

This copies the published app to:

`%LOCALAPPDATA%\Programs\Delivery Note Labeler\`

and registers Explorer menu entries:

**Windows 11:** **Print Labels** on the main right-click menu (when the sparse package was built)

**First-time Windows 11 setup:** the sparse package is signed with a local development certificate. Run this **once as Administrator**, then re-run `install.ps1`:

```powershell
.\scripts\trust-package-certificate.ps1
```

**All versions / fallback:** **Print Labels** under **Show more options** (legacy registry handler)

### Install on other computers

Create a release package on your build machine:

```powershell
.\scripts\package.ps1
```

Copy **`dist\DeliveryNoteLabeler-*-Setup.exe`** to the target PC and double-click it.

Target PCs need **Windows 10/11 64-bit only**. They do **not** need Visual Studio, the Windows SDK, or a separate .NET install.

The release package no longer tries to register the Windows 11 AppX/sparse menu (that path required extra certificates and caused installs to fail on normal PCs). **Print Labels** is registered via the standard Explorer menu instead (on Windows 11 it may appear under **Show more options**).

### Online download and automatic update checks

You can host the installer online and let installed apps detect new versions automatically.

**One-time setup (GitHub):**

1. Push this repo to GitHub.
2. Install [GitHub CLI](https://cli.github.com/) and run `gh auth login`.
3. Enable **GitHub Pages** for the repo (Settings → Pages → deploy from the `/docs` folder).
4. Publish a release:

```powershell
.\scripts\publish-release.ps1 -Repo "your-org/delivery-note-labeler" -ReleaseNotes "Describe what changed."
```

That script:

- Builds `dist\DeliveryNoteLabeler-*-Setup.exe`
- Uploads it to **GitHub Releases**
- Updates `docs/latest.json` for the download page and in-app update checks
- Saves the update URL into `packaging/update/manifest-url.txt` (embedded in future builds)

**Download page:** `https://your-org.github.io/delivery-note-labeler/`

**What users see:** when a newer version is published, the app shows an **Update available** popup on startup (or from **Settings → Check for updates**). **Download update** opens the online installer in the browser.

To point an existing build at a different update server without rebuilding:

```powershell
$env:DELIVERY_NOTE_LABELER_UPDATE_URL = "https://your-server.example/latest.json"
```

Or host `latest.json` and `DeliveryNoteLabeler-*-Setup.exe` on any HTTPS server (SharePoint, Azure Blob, internal web server). The manifest format is:

```json
{
  "version": "1.0.3",
  "releaseDate": "2026-06-08",
  "downloadUrl": "https://example.com/DeliveryNoteLabeler-1.0.3-Setup.exe",
  "releaseNotes": "What changed in this release."
}
```

Manual update still works: build a new package and run the new Setup.exe on each PC.

Multi-select passes all selected files to **one app window**. The app scans each PDF into a **delivery note queue** on the left. Select a note to review its label preview on the right, then use **Print** to confirm and move to the next note, or **Print All** to process the whole queue.

**Print** and **Print All** send labels to the configured printer as RAW ZPL, then remove notes from the queue.

## GK420d network printing (USB printer, multiple PCs)

The GK420d is USB-only. To print from multiple workstations, connect it to a **dedicated always-on Windows PC** (the print host) and **share the printer** on your network. Each workstation installs that shared printer and selects it in **Settings**.

| Step | Print host PC | Each workstation |
|------|---------------|------------------|
| 1 | Install the Zebra GK420d driver and calibrate media | — |
| 2 | Share the printer (e.g. share name `ZebraLabels`) | — |
| 3 | Keep the PC awake; disable sleep while printing | — |
| 4 | On the shared printer **Sharing** tab, turn off **Render print jobs on client computers** | — |
| 5 | — | Add network printer `\\PrintHost\ZebraLabels` |
| 6 | — | Open app **Settings**, select the printer, click **Test print** |

The app sends **RAW ZPL** directly to the Windows spooler. Use the Zebra driver on the print host (Generic / Text Only only for testing).

Optional environment variable override:

```powershell
$env:DELIVERY_NOTE_LABELER_PRINTER = "\\PrintHost\ZebraLabels"
```

Printer settings are stored in `%USERPROFILE%\.delivery-note-labeler\config.json`. Defaults match **4×2 inch** media on a GK420d at 203 dpi (`label_width_dots`: 812, `label_height_dots`: 406).

```json
{
  "printer_name": "\\\\PrintHost\\ZebraLabels",
  "label_width_dots": 812,
  "label_height_dots": 406
}
```

### Printing troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Blank labels | Driver re-rendered the job | Use RAW passthrough; disable render-on-client on the share |
| Cannot open printer | Wrong name or offline host | Reinstall shared printer; verify print host is on |
| Access denied | Share permissions | Grant Print access on the host share |
| Description cut off | Label too small for text | Adjust media size in config (`label_width_dots` / `label_height_dots`) |

### Uninstall

```powershell
.\scripts\uninstall.ps1
```

Removes the install folder and the Explorer context menu entry.

## Usage

1. Drag and drop a delivery note PDF anywhere on the window, click **Browse PDF**, or use **Print Labels** from File Explorer.
2. Wait for PDFs to scan into the **delivery note queue** on the left.
3. Select a note, review and edit label rows in the table if needed.
4. Click **Print** to send labels for the current note, or **Print All** for every ready note.
5. Optionally click **Export CSV** to save the current note’s label jobs.

Configure the GK420d in **Settings** before printing (see network printing section above).

Each line item is expanded by quantity. For example, a line with `2 ea` produces two label rows.

### Scanned / photocopied PDFs

The app always tries **normal scan** first (fast, local, no API). If that cannot extract line items, it automatically tries **AI scan** when a Gemini API key is configured.

While a PDF is loading, the status bar shows which step is running, for example **Scanning with normal mode…** or **Normal scan found nothing — scanning with AI…**.

1. Get an API key from [Google AI Studio](https://aistudio.google.com/apikey)
2. In AI Studio, open **Settings → Plan / billing** and **link a billing account** to the project. Google often shows “quota exceeded (limit: 0)” on the first AI scan until billing is linked — you are not charged for normal test usage on the free tier.
3. Create a **new API key** in that project after billing is linked.
4. Open **Settings** in the app and paste the key, **or** set an environment variable:

```powershell
$env:GEMINI_API_KEY = "your-key-here"
```

5. Drop or browse for your PDF.

The loaded file summary shows **Normal scan** or **AI scan (model name)** when complete.

Existing Gemini keys in `%USERPROFILE%\.delivery-note-labeler\config.json` continue to work after migration from the Python version.

## Sample PDF

The parser was built against:

`C:\Users\brook\Documents\Delivery Note Scan\deliverynote004223 rev 1.pdf`

Expected results:

- Delivery note: `004223 rev 1`
- Customer order: `4507425575`
- 5 line items
- 7 label jobs after quantity expansion

## Run tests

```powershell
dotnet test DeliveryNoteLabeler.slnx -c Release
```

## Project structure

```
delivery-note-labeler/
  DeliveryNoteLabeler.slnx
  src/
    DeliveryNoteLabeler.Core/     # Models, PDF extraction, Gemini, CSV, ZPL, config
    DeliveryNoteLabeler/          # WPF app (MainWindow, Settings, single-instance)
  tests/
    DeliveryNoteLabeler.Core.Tests/
  scripts/
    build.ps1                     # dotnet publish + Win11 shell extension
    package.ps1                   # self-contained release folder + zip for other PCs
    build-sparse-package.ps1      # sparse MSIX for Win11 menu
    register-sparse-package.ps1   # register/remove sparse package
    trust-package-certificate.ps1 # one-time admin cert trust for sparse package
    install.ps1                   # Install exe + Explorer menu (this PC, from dist\publish)
    uninstall.ps1                 # Remove install + menu
  packaging/install/              # Standalone Install.ps1 copied into release packages
  packaging/sparse/               # AppxManifest for Win11 context menu
  src/DeliveryNoteLabeler.ShellExtension/  # IExplorerCommand COM DLL
  dist/
    publish/                      # Published exe output
```

## Phase 2 (printing)

Implemented:

- ZPL label generation from parsed/edited label jobs (`DeliveryNoteLabeler.Core/Printing/`)
- RAW Windows spooler output to a configured printer name
- Settings: printer picker, test print, config in `config.json`
- Print / Print All integrated with the review queue

Still planned:

- ZebraDesigner template import (replace built-in layout)
- Barcode / QR on labels
- Signed MSI for enterprise rollout (Intune/SCCM)

## Extracted fields

| Label field | PDF source |
|-------------|------------|
| Delivery note no. | `Delivery Note No.` |
| Order no. | `Customer Order No.` |
| Part no. | Line `Drawing No.` |
| Quantity | Expanded per physical unit |

Multi-page delivery notes are supported: headers are read from page 1, line items are merged from all pages.
