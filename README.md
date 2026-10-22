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
- **Windows Error Reporting** archives/queues · **Shader Cache** (DirectX, NVIDIA, AMD, Intel)
- **Windows Upgrade Files** (`$Windows.~BT`, `$Windows.~WS`, ESD) and **Old Windows Installation**
  (`Windows.old`, ⚠ off by default — removes rollback) via the built-in Disk Cleanup handlers
- **Turn Off Hibernation** — removes `hiberfil.sys` via `powercfg /hibernate off` (⚠ off by default)
- **Component Store (WinSxS)** via `DISM /StartComponentCleanup` (⚠ off by default, slow) — WinSxS is never touched directly

### Developer Cleanup
- **npm Cache** · **Yarn Cache** · **Composer Cache** · **pnpm Cache** · **Bun Cache** · **pip Cache**
- **Gradle Cache** (dependencies + downloaded Gradle dists) & **NuGet HTTP Cache** & **Maven Wrapper Cache**
- **Flutter Pub Cache** (re-resolved by `flutter pub get`)
- **Android Studio / JetBrains IDE Caches** (caches + logs, all installed products)
- **VS Code Cache** (Cache / CachedData / Code Cache / GPUCache / logs)
- **AI Model Cache (Hugging Face)** — re-downloadable models/blobs (⚠ off by default;
  chat & session history is always preserved)
- **Playwright Browsers** (⚠ re-downloaded on next run, off by default)
- **Docker** build cache, unused images and stopped containers via `docker system df` + `prune`;
  **unused volumes** listed separately (⚠ off by default — may contain app data). Skipped when Docker isn't running
- **Compact Docker Disk** — shrinks Docker's WSL `.vhdx` with diskpart so pruned space returns to C: (⚠ off by default, close Docker first)
- **Chrome & Edge** caches across *all* profiles (`Cache`, `Code Cache`, `GPUCache`) — bookmarks, history, cookies and passwords are never touched

### Application Cleanup
- **Microsoft Teams** (Cache / Code Cache / GPUCache / blob_storage / logs)
- **WPS Office** — old version folders left by auto-update (newest/running kept), downloaded update installers, cache, logs
- **npm global update leftovers** (`node_modules\.pkg-XXXXXXXX` folders from interrupted updates)
- **Slack** · **Discord** caches — chat history and signed-in data are never touched

### Disk Analyzer
- Drive-level folder size breakdown with recursive drill-down
- Live drive usage context (`used 95.7 of 100 GB`)
- Protected system files (`hiberfil.sys`, `pagefile.sys`, …) clearly tagged `SYSTEM`

### Safety & Experience
- **Scan first, delete second** — two-step flow with per-category selection
- Live **progress bar** while scanning and cleaning; all controls lock during work
- **SCAN runs as administrator** (relaunches elevated and starts scanning), so CLEAN never needs a second permission prompt + rescan
- Categories with nothing to clean (tool not installed, cache empty) are hidden
- Grouped results: **System Cleanup** / **Developer Cleanup** / **Application Caches** with live "ready to clean" total
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
> 73 unit tests cover the domain model, safety checks, cleaning engine,
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

- Developer Mode (auto-detect installed toolchains)
- Large Files view in the Disk Analyzer
- System Restore space info
- Stopwatch & space-recovered history dashboard