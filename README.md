<div align="center">

# 🧹 PC Cleaner

**A developer-focused Windows junk cleaner built with C# / .NET 8 / WPF**

Scans, reports, and safely removes system and developer caches — so you always
know exactly what is eating your disk before you delete anything.

</div>

---

## ✨ Features

### System Cleanup
- **User Temp** & **Windows Temp**
- **Recycle Bin** (all fixed drives)
- **Thumbnail Cache** & **Icon Cache**
- **Crash Dumps** (local + minidump + MEMORY.DMP)
- **Windows Update Cache** & **Delivery Optimization**

### Developer Cleanup
- **npm Cache** · **Yarn Cache** · **Composer Cache**
- **VS Code Cache** (Cache / CachedData / Code Cache / GPUCache / logs)
- **Playwright Browsers** (⚠ re-downloaded on next run, off by default)
- **Chrome & Edge** caches across *all* profiles (`Cache`, `Code Cache`, `GPUCache`) — bookmarks, history, cookies and passwords are never touched

### Disk Analyzer
- Drive-level folder size breakdown with recursive drill-down
- Live drive usage context (`used 95.7 of 100 GB`)
- Protected system files (`hiberfil.sys`, `pagefile.sys`, …) clearly tagged `SYSTEM`

### Safety & Experience
- **Scan first, delete second** — two-step flow with per-category selection
- Live **progress bar** while scanning and cleaning; all controls lock during work
- Admin elevation requested only when needed (Windows Temp, Windows Update, Delivery Optimization)
- Grouped results: **System Cleanup** / **Developer Cleanup** with live "ready to clean" total
- Detailed cleaning report (deleted / skipped / reasons)

---

## 🚀 Getting Started

### Prerequisites
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (`net8.0-windows`)
- Windows 10/11 (x64)
- Optional: [Inno Setup 6](https://jrsoftware.org/isinfo.php) to build the installer

### Build & run
```bash
git clone <repo-url>
cd PC-Cleaner
dotnet restore
dotnet build -c Release
dotnet run --project src/PcCleaner.App
```

### Run the tests
```bash
dotnet test
```
> 43 unit tests cover the domain model, safety checks, cleaning engine,
> scanners (incl. all-profile browser discovery), the disk analyzer, and
> progress reporting.

### Build the installer (optional)
```powershell
# 1. Publish a self-contained output
dotnet publish src/PcCleaner.App -c Release -r win-x64 --self-contained false -o src/PcCleaner.App/bin/Release/net8.0-windows/publish

# 2. Compile the install script
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer/pc-cleaner.iss
```
Output: `installer/PC-Cleaner-Setup.exe`

---

## 🏗 Architecture

Clean layered architecture — `App → Infrastructure → Domain` and `App → Application → Domain`.

```
src/
├── PcCleaner.Domain        # Models, enums, interfaces (zero dependencies)
├── PcCleaner.Application   # Services & app-level contracts (IScanService, progress)
├── PcCleaner.Infrastructure# Scanners, cleaning engine, disk analyzer, admin helper
└── PcCleaner.App           # WPF UI + dependency injection + Serilog wiring
tests/
└── PcCleaner.UnitTests     # xUnit test suite
```

### Key invariants
- Every scanner implements `IScanner` (`Name`, `Type`, `RequiresAdmin`, `ScanAsync`)
- All deletion funnels through the shared `CleaningEngine` (`ICleaner`) with reason-aware error handling (access denied / in use / not found)
- Progress is reported via `IProgress<T>` (`ScanProgress`, `CleanUpProgress`) so the UI never freezes
- Deletion is always explicit: items are scanned first, shown to the user, then deleted — **never** blindly

### Logging
Serilog file sink → `%LOCALAPPDATA%\PC-Cleaner\logs\cleaner-YYYYMMDD.log` (7-day retention).

---

## 💻 How to use

1. Click **SCAN** — categories are scanned with live progress
2. Review the grouped results and uncheck anything you want to keep
   (Playwright is off by default)
3. Click **CLEAN** — confirm, and watch the progress bar
4. Use **DISK ANALYZER ▸** to hunt down the largest folders on any drive
5. Done — click **SCAN** again to rescan

> **Note:** cleaning intents that require admin rights (Windows Temp / Update /
> Delivery Optimization) will prompt to restart as administrator. Choose **No**
> to clean only the non-admin categories.

---

## 🤝 Contributing

1. Fork the repository
2. Follow the existing scanner pattern (see `Infrastructure/Scanners`)
3. Cover new behavior with a unit test (temp-dir fixtures, like the existing suite)
4. `dotnet build` and `dotnet test` must stay green
5. Open a pull request

---

## 🗺 Roadmap

- Docker cleanup (build cache, unused images/containers) via CLI
- WiX / System Cleanup integration (DISM WinSxS, `cleanmgr` for Windows.old)
- pnpm / Bun / NuGet / pip / Gradle cache support
- Stopwatch & space-recovered history dashboard