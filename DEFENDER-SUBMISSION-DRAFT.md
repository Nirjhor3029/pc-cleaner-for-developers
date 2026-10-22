# Microsoft Defender False Positive Submission — Draft

Use this at https://www.microsoft.com/wdsi/filesubmission

**Submission Type:** Software developer
**I believe this file is incorrectly detected as malware/PUA**

**Files to upload:**
- `installer/PC-Cleaner-Setup.exe` (48.82 MB, self-contained)
- `installer/PC-Cleaner-v1.0.0-WhatsApp.zip` (48.3 MB)

**Details to paste:**

```
Application: PC Cleaner v1.0.0 — Developer-focused Windows junk cleaner (C# / .NET 8 / WPF)
Publisher: PcCleaner (https://github.com/Nirjhor3029/pc-cleaner-for-developers)
Repo: https://github.com/Nirjhor3029/pc-cleaner-for-developers (public, MIT, open source)
File: PC-Cleaner-Setup.exe — Inno Setup 6 installer, self-contained, no network exfiltration
Behavior: Scans and deletes temp/cache files only (User Temp, Windows Temp, Recycle Bin, Thumbnail Cache, Windows Update Cache, npm/yarn/pnpm/Bun/pip/Composer/Gradle/NuGet/Maven/Flutter pub, VS Code caches, Chrome/Edge cache, Teams/Slack/Discord cache, HuggingFace/Playwright blobs). All deletion is explicit: scan first, show to user, then delete via CleaningEngine. Requires admin only for Windows Temp/Update/Delivery Optimization.
False detection: Defender flags as PUA:Win32/Cleaner or SmartScreen Unknown publisher because unsigned + requests admin + deletes temp files. This is legitimate cleaner behavior, no malware, no persistence, no stealing, no network calls beyond local scan.
Please whitelist hash and publisher. Signed build via SignPath (free OSS) pending.
Contact: sazzad3029@gmail.com
```

**After submission:** Microsoft reviews in 24-48h, whitelists hash. Future Defender won't block.

**Also submit ZIP separately as "Not malware" for download reputation.**
