# Migrating from Squirrel.Windows to Burrow

This guide walks you through upgrading an existing desktop application from **Squirrel.Windows** to **Burrow**.

---

## 1. Overview & What's Changed

**Burrow** is the modernized, high-performance evolution of Squirrel.Windows. It provides broad compatibility with existing Squirrel APIs, installations, and command-line tools while delivering major speed, memory, and architecture improvements. Its `RELEASES` reader accepts both legacy text and versioned JSON formats; newly written JSON `RELEASES` files require Burrow-compatible clients.

| Feature | Legacy Squirrel.Windows | Burrow |
| :--- | :--- | :--- |
| **Delta Engine** | BSDiff + BZip2 (via SharpCompress) | **Zstandard (`ZstdSharp`)** |
| **Delta Decompress Speed** | ~15 – 30 MB/s | **~800 – 1,200 MB/s (~25x–40x faster)** |
| **Package Inspection RAM** | Buffers entire `.nupkg` (100MB+ in RAM) | **On-demand stream reader (~1–5 MB RAM)** |
| **Legacy Dependencies** | Bundles NuGet 2.x, WCF Data Services, SharpCompress | **Zero legacy dependencies (native .NET BCL + Zstd)** |
| **Codebase Footprint** | ~2,200+ source files | **~350 source files (~84% reduction)** |
| **Target Frameworks** | .NET Framework 4.5+ | **.NET Framework 4.8 / .NET Standard 2.0** |

---

## 2. Step-by-Step Migration Guide

### Step 1: Update NuGet Package Reference

In your application's project file (`.csproj`), replace the `squirrel.windows` dependency with `burrow.windows`:

#### PackageReference (SDK-style projects):
```xml
- <PackageReference Include="squirrel.windows" Version="2.0.1" />
+ <PackageReference Include="burrow.windows" Version="2.0.1" />
```

#### packages.config (Legacy projects):
```xml
- <package id="squirrel.windows" version="2.0.1" targetFramework="net48" />
+ <package id="burrow.windows" version="2.0.1" targetFramework="net48" />
```

---

### Step 2: Application Code (No Changes Required)

Burrow maintains complete namespace and type compatibility (`Squirrel.UpdateManager`, `Squirrel.SquirrelAwareApp`, `Squirrel.IUpdateManager`, etc.). Your existing C# update logic remains identical:

```csharp
using Squirrel; // Retained for 100% source compatibility

// Initializing the update manager
using (var mgr = new UpdateManager("https://updates.example.com/releases"))
{
    // Auto-update works as usual:
    await mgr.UpdateApp();
}

// Handling Squirrel lifecycle hooks on install/update/uninstall
SquirrelAwareApp.HandleEvents(
    onInitialInstall: v => mgr.CreateShortcutForThisExe(),
    onAppUpdate: v => mgr.CreateShortcutForThisExe(),
    onAppUninstall: v => mgr.RemoveShortcutForThisExe());
```

---

### Step 3: Update CI/CD Packaging Scripts

Replace invocations of `Squirrel.exe` (or `Update.exe`) in your release scripts with `Burrow.exe`:

#### Before:
```cmd
Squirrel.exe --releasify MyApp.1.0.0.nupkg --releaseDir .\Releases
```

#### After:
```cmd
Burrow.exe --releasify MyApp.1.0.0.nupkg --releaseDir .\Releases
```

All standard CLI arguments (`--releasify`, `--releaseDir`, `--packagesDir`, `--bootstrapperExe`, `--no-msi`, `--signWithParams`) work identically.

---

## 3. Transitioning Existing Installed Clients

### Can users who installed with legacy Squirrel update to Burrow releases?
**Yes, but the `RELEASES` format must match the client.**
- Burrow reads both legacy text `RELEASES` files and versioned JSON `RELEASES` files.
- New Burrow writers produce the versioned JSON format while keeping the filename `RELEASES`.
- Legacy Squirrel clients cannot parse a newly written JSON `RELEASES` file unchanged.
- A feed shared with legacy Squirrel clients must continue publishing a legacy text `RELEASES` file, or those clients must migrate before switching the feed file to JSON.

### Can Burrow apply older legacy Squirrel delta patches?
**Yes.** Burrow's `BinaryPatchUtility` includes automatic fallback support to decompress legacy `BSDIFF40` patches with Deflate, BZip2 (`SharpZipLib`), and modern Zstandard patches.
---

## 4. Performance & Memory Impact

### Delta Patching Benchmarks
Applying a 50MB delta package across 20 assemblies:
- **Legacy Squirrel**: ~8–15 seconds of sustained 100% CPU core usage.
- **Burrow**: **< 400 milliseconds** with minimal CPU usage.

### Packaging Memory Usage
- **Legacy Squirrel**: 200MB–600MB peak working set during `--releasify` on large packages due to in-memory `.nupkg` archive duplication.
- **Burrow**: **< 25MB peak working set** using stream-based on-demand archive processing.

---

## 5. Frequently Asked Questions (FAQ)

#### Do I need to change how I sign my executables?
No. Code signing via `--signWithParams` or `signtool.exe` operates identically.

#### What happens to my custom URL downloaders or HTTP clients?
`IFileDownloader` is fully preserved. Any custom implementation of `IFileDownloader` passed to `new UpdateManager(url, appName, rootDir, customDownloader)` continues to work without modification.

#### Is MSI generation still supported?
Yes. Burrow supports `--createMsi` or `--no-msi` just like legacy Squirrel.
