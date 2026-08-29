using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet;
using Squirrel.Update;
using Squirrel.SimpleSplat;
namespace Squirrel.Cli.Commands
{
    public class ReleasifySettings : GlobalSettings
    {
        [CommandArgument(0, "[PACKAGE]")]
        [Description("NuGet package file (.nupkg)")]
        public string PackageArg { get; set; }

        [CommandOption("-p|--package <PACKAGE>")]
        [Description("NuGet package file (.nupkg)")]
        public string PackageOption { get; set; }

        public string Package { get => PackageOption ?? PackageArg; set => PackageOption = value; }

        [CommandOption("-r|--release-dir <DIR>")]
        [Description("Path to a release directory")]
        public string ReleaseDir { get; set; }

        [CommandOption("--packages-dir <DIR>")]
        [Description("Path to the NuGet Packages directory")]
        public string PackagesDir { get; set; }

        [CommandOption("--bootstrapper-exe <EXE>")]
        [Description("Path to the Setup.exe to use as a template")]
        public string BootstrapperExe { get; set; }

        [CommandOption("-g|--loading-gif <GIF>")]
        [Description("Path to an animated GIF to be displayed during installation")]
        public string LoadingGif { get; set; }

        [CommandOption("-i|--icon <ICO>")]
        [Description("Path to an ICO file for shortcuts")]
        public string Icon { get; set; }

        [CommandOption("--setup-icon <ICO>")]
        [Description("Path to an ICO file for the Setup executable's icon")]
        public string SetupIcon { get; set; }

        [CommandOption("-n|--sign-with-params <PARAMS>")]
        [Description("Sign the installer via SignTool.exe with the parameters given")]
        public string SigningParameters { get; set; }

        [CommandOption("-b|--base-url <URL>")]
        [Description("Base URL to prefix the RELEASES file packages with")]
        public string BaseUrl { get; set; }

        [CommandOption("--no-msi")]
        [Description("Don't generate an MSI package")]
        public bool NoMsi { get; set; }

        [CommandOption("--no-delta")]
        [Description("Don't generate delta packages")]
        public bool NoDelta { get; set; }

        [CommandOption("--framework-version <VER>")]
        [Description("Set the required .NET framework version (e.g. net461)")]
        public string FrameworkVersion { get; set; } = "net45";

        [CommandOption("--msi-win64")]
        [Description("Mark the MSI as 64-bit")]
        public bool MsiWin64 { get; set; }

        [CommandOption("--update-only")]
        [Description("Update shortcuts that already exist, rather than creating new ones")]
        public bool UpdateOnly { get; set; }
    }
    public sealed class ReleasifyCommand : CommandBase<ReleasifySettings>, IEnableLogger

    {
        protected override int ExecuteCommand(ReleasifySettings settings)
        {
            ValidateRequired(settings.Package, "<PACKAGE>", "Update.exe releasify myapp.nupkg --release-dir ./Releases");
            ValidatePathExists(settings.Package!, "<PACKAGE>");

            EnsureConsole();

            if (settings.BaseUrl != null)
            {
                if (!Squirrel.Utility.IsHttpUrl(settings.BaseUrl))
                {
                    throw new ValidationError(
                        $"Invalid --base-url '{settings.BaseUrl}'. A base URL must start with http or https and be a valid URI.",
                        "--base-url");
                }

                if (!settings.BaseUrl.EndsWith("/"))
                {
                    settings.BaseUrl += "/";
                }
            }

            var targetDir = settings.ReleaseDir ?? Path.Combine(".", "Releases");
            var packagesDir = settings.PackagesDir ?? ".";
            var bootstrapperExe = settings.BootstrapperExe ?? Path.Combine(".", "Setup.exe");

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (!File.Exists(bootstrapperExe))
            {
                bootstrapperExe = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                    "Setup.exe");
            }

            Context.Log().Info("Bootstrapper EXE found at: " + bootstrapperExe);

            var di = new DirectoryInfo(targetDir);
            var targetPackagePath = Path.Combine(di.FullName, Path.GetFileName(settings.Package!));
            File.Copy(settings.Package!, targetPackagePath, true);

            var allNuGetFiles = di.EnumerateFiles()
                .Where(x => x.Name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));

            var toProcess = allNuGetFiles.Where(x => !x.Name.Contains("-delta") && !x.Name.Contains("-full"));
            var processed = new List<string>();

            var releaseFilePath = Path.Combine(di.FullName, "RELEASES");
            var previousReleases = new List<Squirrel.ReleaseEntry>();
            if (File.Exists(releaseFilePath))
            {
                previousReleases.AddRange(Squirrel.ReleaseEntry.ParseReleaseFile(File.ReadAllText(releaseFilePath, Encoding.UTF8)));
            }

            var progressTask = Progress.AddTask("Creating release packages...", maxValue: toProcess.Count());

            foreach (var file in toProcess)
            {
                Context.Log().Info("Creating release package: " + file.FullName);
                Progress.Update(progressTask, processed.Count, $"Processing {file.Name}...");

                var rp = new Squirrel.ReleasePackage(file.FullName);
                rp.CreateReleasePackage(Path.Combine(di.FullName, rp.SuggestedReleaseFileName), packagesDir, contentsPostProcessHook: pkgPath =>
                {
                    new DirectoryInfo(pkgPath).GetAllFilesRecursively()
                        .Where(x => x.Name.ToLowerInvariant().EndsWith(".exe"))
                        .Where(x => !x.Name.ToLowerInvariant().Contains("squirrel.exe"))
                        .Where(x => Squirrel.Utility.IsFileTopLevelInPackage(x.FullName, pkgPath))
                        .Where(x => Squirrel.Utility.ExecutableUsesWin32Subsystem(x.FullName))
                        .ForEachAsync(x => CreateExecutableStubForExe(x.FullName))
                        .Wait();

                    if (settings.SigningParameters == null) return;

                    new DirectoryInfo(pkgPath).GetAllFilesRecursively()
                        .Where(x => Squirrel.Utility.FileIsLikelyPEImage(x.Name))
                        .ForEachAsync(async x =>
                        {
                            if (IsPEFileSigned(x.FullName))
                            {
                                Context.Log().Info("{0} is already signed, skipping", x.FullName);
                                return;
                            }

                            Context.Log().Info("About to sign {0}", x.FullName);
                            await SignPEFile(x.FullName, settings.SigningParameters!);
                        }, 1)
                        .Wait();
                });

                processed.Add(rp.ReleasePackageFile);

                var prev = Squirrel.ReleaseEntry.GetPreviousRelease(previousReleases, rp, targetDir);
                if (prev != null && !settings.NoDelta)
                {
                    var deltaBuilder = new Squirrel.DeltaPackageBuilder(null);

                    var dp = deltaBuilder.CreateDeltaPackage(prev, rp,
                        Path.Combine(di.FullName, rp.SuggestedReleaseFileName.Replace("full", "delta")));
                    processed.Insert(0, dp.InputPackageFile);
                }

                Progress.Increment(progressTask);
            }

            Progress.Finish(progressTask);

            foreach (var file in toProcess)
            {
                File.Delete(file.FullName);
            }

            var newReleaseEntries = processed
                .Select(packageFilename => Squirrel.ReleaseEntry.GenerateFromFile(packageFilename, settings.BaseUrl))
                .ToList();

            var distinctPreviousReleases = previousReleases
                .Where(x => !newReleaseEntries.Select(e => e.Version).Contains(x.Version));
            var releaseEntries = distinctPreviousReleases.Concat(newReleaseEntries).ToList();

            Squirrel.ReleaseEntry.WriteReleaseFile(releaseEntries, releaseFilePath);

            var targetSetupExe = Path.Combine(di.FullName, "Setup.exe");
            var newestFullRelease = releaseEntries.MaxBy(x => x.Version).Where(x => !x.IsDelta).First();

            File.Copy(bootstrapperExe, targetSetupExe, true);

            var zipPath = CreateSetupEmbeddedZip(
                Path.Combine(di.FullName, newestFullRelease.Filename),
                di.FullName,
                settings.LoadingGif,
                settings.SigningParameters,
                settings.SetupIcon).Result;

            var writeZipToSetup = Squirrel.Utility.FindHelperExecutable("WriteZipToSetup.exe");

            try
            {
                var arguments = String.Format("\"{0}\" \"{1}\" \"--set-required-framework\" \"{2}\"",
                    targetSetupExe, zipPath, settings.FrameworkVersion);
                var result = Squirrel.Utility.InvokeProcessAsync(writeZipToSetup, arguments, CancellationToken.None).Result;
                if (result.Item1 != 0)
                {
                    throw new Exception("Failed to write Zip to Setup.exe!\n\n" + result.Item2);
                }
            }
            catch (Exception ex)
            {
                Context.Log().ErrorException("Failed to update Setup.exe with new Zip file", ex);
            }
            finally
            {
                File.Delete(zipPath);
            }

            Squirrel.Utility.Retry(() =>
                SetPEVersionInfoAndIcon(targetSetupExe, new ZipPackage(settings.Package!), settings.SetupIcon).Wait());

            if (settings.SigningParameters != null)
            {
                SignPEFile(targetSetupExe, settings.SigningParameters).Wait();
            }

            if (!settings.NoMsi)
            {
                CreateMsiPackage(targetSetupExe, new ZipPackage(settings.Package!), settings.MsiWin64).Wait();

                if (settings.SigningParameters != null)
                {
                    SignPEFile(targetSetupExe.Replace(".exe", ".msi"), settings.SigningParameters).Wait();
                }
            }

            Context.WriteSuccess($"Releases directory updated at {targetDir}");
            return 0;
        }

        async Task<string> CreateSetupEmbeddedZip(string fullPackage, string releasesDir, string backgroundGif, string signingOpts, string setupIcon)
        {
            string tempPath;

            Context.Log().Info("Building embedded zip file for Setup.exe");
            using (Squirrel.Utility.WithTempDirectory(out tempPath, null))
            {
                this.ErrorIfThrows(() =>
                {
                    File.Copy(Assembly.GetEntryAssembly().Location.Replace("-Mono.exe", ".exe"), Path.Combine(tempPath, "Update.exe"));
                    File.Copy(fullPackage, Path.Combine(tempPath, Path.GetFileName(fullPackage)));
                }, "Failed to write package files to temp dir: " + tempPath);

                if (!String.IsNullOrWhiteSpace(backgroundGif))
                {
                    this.ErrorIfThrows(() =>
                    {
                        File.Copy(backgroundGif, Path.Combine(tempPath, "background.gif"));
                    }, "Failed to write animated GIF to temp dir: " + tempPath);
                }

                if (!String.IsNullOrWhiteSpace(setupIcon))
                {
                    this.ErrorIfThrows(() =>
                    {
                        File.Copy(setupIcon, Path.Combine(tempPath, "setupIcon.ico"));
                    }, "Failed to write icon to temp dir: " + tempPath);
                }

                var releases = new[] { Squirrel.ReleaseEntry.GenerateFromFile(fullPackage) };
                Squirrel.ReleaseEntry.WriteReleaseFile(releases, Path.Combine(tempPath, "RELEASES"));

                var target = Path.GetTempFileName();
                File.Delete(target);

                if (signingOpts != null)
                {
                    var dir = new DirectoryInfo(tempPath);

                    var files = dir.EnumerateFiles()
                        .Where(x => x.Name.ToLowerInvariant().EndsWith(".exe"))
                        .Select(x => x.FullName);

                    await files.ForEachAsync(x => SignPEFile(x, signingOpts));
                }

                this.ErrorIfThrows(() =>
                    ZipFile.CreateFromDirectory(tempPath, target, CompressionLevel.Optimal, false),
                    "Failed to create Zip file from directory: " + tempPath);

                return target;
            }
        }

        async Task SignPEFile(string exePath, string signingOpts)
        {
            var exe = @".\signtool.exe";
            if (!File.Exists(exe))
            {
                exe = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "signtool.exe");

                if (!File.Exists(exe)) exe = "signtool.exe";
            }

            var processResult = await Squirrel.Utility.InvokeProcessAsync(exe,
                String.Format("sign {0} \"{1}\"", signingOpts, exePath), CancellationToken.None);

            if (processResult.Item1 != 0)
            {
                var optsWithPasswordHidden = new Regex(@"(?x)
                    (?i)
                    (?<=/p\s+)
                    .*?
                    (?=\s+)
                ").Replace(signingOpts, "/p ********");

                var msg = String.Format("Failed to sign, command invoked was: '{0} sign {1} {2}'",
                    exe, optsWithPasswordHidden, exePath);

                throw new Exception(msg);
            }
        }

        bool IsPEFileSigned(string path)
        {
#if MONO
            return Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase);
#else
            try
            {
                return Squirrel.AuthenticodeTools.IsTrusted(path);
            }
            catch (Exception ex)
            {
                Context.Log().ErrorException("Failed to determine signing status for " + path, ex);
                return false;
            }
#endif
        }

        async Task CreateExecutableStubForExe(string fullName)
        {
            var exe = Squirrel.Utility.FindHelperExecutable(@"StubExecutable.exe");

            var target = Path.Combine(
                Path.GetDirectoryName(fullName),
                Path.GetFileNameWithoutExtension(fullName) + "_ExecutionStub.exe");

            await Squirrel.Utility.CopyToAsync(exe, target);

            await Squirrel.Utility.InvokeProcessAsync(
                Squirrel.Utility.FindHelperExecutable("WriteZipToSetup.exe"),
                String.Format("--copy-stub-resources \"{0}\" \"{1}\"", fullName, target),
                CancellationToken.None);
        }

        async Task SetPEVersionInfoAndIcon(string exePath, IPackage package, string iconPath = null)
        {
            var realExePath = Path.GetFullPath(exePath);
            var company = String.Join(",", package.Authors);
            var verStrings = new Dictionary<string, string>()
            {
                { "CompanyName", company },
                { "LegalCopyright", package.Copyright ?? "Copyright © " + DateTime.Now.Year.ToString() + " " + company },
                { "FileDescription", package.Summary ?? package.Description ?? "Installer for " + package.Id },
                { "ProductName", package.Description ?? package.Summary ?? package.Id },
            };

            var args = verStrings.Aggregate(new StringBuilder("\"" + realExePath + "\""), (acc, x) =>
            {
                acc.AppendFormat(" --set-version-string \"{0}\" \"{1}\"", x.Key, x.Value);
                return acc;
            });
            args.AppendFormat(" --set-file-version {0} --set-product-version {0}", package.Version.ToString());
            if (iconPath != null)
            {
                args.AppendFormat(" --set-icon \"{0}\"", Path.GetFullPath(iconPath));
            }

            string exe = Squirrel.Utility.FindHelperExecutable("rcedit.exe");

            var processResult = await Squirrel.Utility.InvokeProcessAsync(exe, args.ToString(), CancellationToken.None);

            if (processResult.Item1 != 0)
            {
                var msg = String.Format(
                    "Failed to modify resources, command invoked was: '{0} {1}'\n\nOutput was:\n{2}",
                    exe, args, processResult.Item2);

                throw new Exception(msg);
            }
        }

        async Task CreateMsiPackage(string setupExe, IPackage package, bool packageAs64Bit)
        {
            var pathToWix = PathToWixTools();
            var setupExeDir = Path.GetDirectoryName(setupExe);
            var company = String.Join(",", package.Authors);

            var culture = CultureInfo.GetCultureInfo(package.Language ?? "").TextInfo.ANSICodePage;

            var templateText = File.ReadAllText(Path.Combine(pathToWix, "template.wxs"));
            var templateData = new Dictionary<string, string>
            {
                { "Id", package.Id },
                { "Title", package.Title },
                { "Author", company },
                { "Version", Regex.Replace(package.Version.ToString(), @"-.*$", "") },
                { "Summary", package.Summary ?? package.Description ?? package.Id },
                { "Codepage", $"{culture}" },
                { "Platform", packageAs64Bit ? "x64" : "x86" },
                { "ProgramFilesFolder", packageAs64Bit ? "ProgramFiles64Folder" : "ProgramFilesFolder" },
                { "Win64YesNo", packageAs64Bit ? "yes" : "no" }
            };

            for (int i = 1; i <= 10; i++)
            {
                templateData[String.Format("IdAsGuid{0}", i)] = Squirrel.Utility.CreateGuidFromHash(String.Format("{0}:{1}", package.Id, i)).ToString();
            }

            var templateResult = CopStache.Render(templateText, templateData);

            var wxsTarget = Path.Combine(setupExeDir, "Setup.wxs");
            File.WriteAllText(wxsTarget, templateResult, Encoding.UTF8);

            var candleParams = String.Format("-nologo -ext WixNetFxExtension -out \"{0}\" \"{1}\"",
                wxsTarget.Replace(".wxs", ".wixobj"), wxsTarget);
            var processResult = await Squirrel.Utility.InvokeProcessAsync(
                Path.Combine(pathToWix, "candle.exe"), candleParams, CancellationToken.None, setupExeDir);

            if (processResult.Item1 != 0)
            {
                var msg = String.Format(
                    "Failed to compile WiX template, command invoked was: '{0} {1}'\n\nOutput was:\n{2}",
                    "candle.exe", candleParams, processResult.Item2);

                throw new Exception(msg);
            }

            var lightParams = String.Format("-ext WixNetFxExtension -sval -out \"{0}\" \"{1}\"",
                wxsTarget.Replace(".wxs", ".msi"), wxsTarget.Replace(".wxs", ".wixobj"));
            processResult = await Squirrel.Utility.InvokeProcessAsync(
                Path.Combine(pathToWix, "light.exe"), lightParams, CancellationToken.None, setupExeDir);

            if (processResult.Item1 != 0)
            {
                var msg = String.Format(
                    "Failed to link WiX template, command invoked was: '{0} {1}'\n\nOutput was:\n{2}",
                    "light.exe", lightParams, processResult.Item2);

                throw new Exception(msg);
            }

            var toDelete = new[]
            {
                wxsTarget,
                wxsTarget.Replace(".wxs", ".wixobj"),
                wxsTarget.Replace(".wxs", ".wixpdb"),
            };

            await Squirrel.Utility.ForEachAsync(toDelete, x => Squirrel.Utility.DeleteFileHarder(x));
        }

        string PathToWixTools()
        {
            var ourPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (File.Exists(Path.Combine(ourPath, "candle.exe")))
            {
                return ourPath;
            }

            var debugPath = Path.Combine(ourPath, "..", "..", "..", "vendor", "wix");
            if (File.Exists(Path.Combine(debugPath, "candle.exe")))
            {
                return Path.GetFullPath(debugPath);
            }

            throw new Exception("WiX tools can't be found");
        }

        static void EnsureConsole()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;

            if (Interlocked.CompareExchange(ref consoleCreated, 1, 0) == 1) return;

            if (!Squirrel.NativeMethods.AttachConsole(-1))
            {
                Squirrel.NativeMethods.AllocConsole();
            }

            Squirrel.NativeMethods.GetStdHandle(Squirrel.StandardHandles.STD_ERROR_HANDLE);
            Squirrel.NativeMethods.GetStdHandle(Squirrel.StandardHandles.STD_OUTPUT_HANDLE);
        }

        static int consoleCreated = 0;
    }
}