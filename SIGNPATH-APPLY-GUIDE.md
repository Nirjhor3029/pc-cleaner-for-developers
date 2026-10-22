# SignPath Free Code Signing for Open Source — Apply Guide (0 tk)

PC Cleaner is eligible because repo is public.

## Step 1 — Apply (5 min)
1. Go to https://signpath.io/ → Pricing → Free for Open Source → Request
2. Form: Project `PC Cleaner`, URL `https://github.com/Nirjhor3029/pc-cleaner-for-developers`, License `MIT`, Description: `Developer-focused Windows junk cleaner (C# .NET 8 WPF) — scans/cleans system & dev caches (npm, Gradle, VS Code, Chrome, etc.) and disk analyzer. Self-contained Inno Setup installer.`
3. Link to repo, confirm public, maintainers `Nirjhor3029 (sazzad3029@gmail.com)`
4. Wait 2-3 days approval (SignPath Foundation reviews).

## Step 2 — After Approval
- SignPath dashboard gives Organization ID + Project slug
- Add GitHub Secrets: `SIGNPATH_API_TOKEN`
- Add workflow `.github/workflows/sign.yml` (draft already in repo)

## Step 3 — CI Signing Workflow (draft)
Workflow will:
- `dotnet publish -c Release -r win-x64 --self-contained true -o publish`
- `ISCC.exe installer/pc-cleaner.iss` (unsigned)
- Upload `PC-Cleaner-Setup.exe` to SignPath → download signed → attach to GitHub Release `v1.0.0-signed`

Signed file shows `Verified publisher: SignPath Foundation on behalf of PcCleaner` + Defender trust.

## Alternative — Azure Trusted Signing Free Trial (if SignPath rejected)
- Azure Portal → Trusted Signing Account → Create (Identity validation with NID, 1 day)
- Free $200 credit covers ~2000 signs. Use `AzureSignTool`.
- Not needed if SignPath approves.

Keep repo public until signed release is published.
