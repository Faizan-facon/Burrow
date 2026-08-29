using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Squirrel.Cli.Commands;

namespace Squirrel.Cli.Tests
{
    /// <summary>
    /// Base class for CLI command tests using Spectre.Console.Testing
    /// </summary>
    public abstract class CliTestBase : IDisposable
    {
        private static readonly object ConsoleLock = new object();

        protected readonly TestConsole Console;
        protected CommandAppTester App;
        protected readonly string TempDir;
        protected readonly StringBuilder ErrorOutput;
        protected string LastOutput = "";

        protected CliTestBase()
        {
            Console = new TestConsole();
            Console.Profile.Width = 4096;
            ErrorOutput = new StringBuilder();
            TempDir = Path.Combine(Path.GetTempPath(), $"SquirrelCliTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(TempDir);

            ConfigureUpdateApp();
        }

        protected void ConfigureUpdateApp(Action<IConfigurator> configure = null)
        {
            App = new CommandAppTester();
            App.Configure(config =>
            {
                config.SetApplicationName("Update.exe");
                config.ValidateExamples();

                config.AddCommand<InstallCommand>("install")
                    .WithDescription("Install the app from a package directory");

                config.AddCommand<UninstallCommand>("uninstall")
                    .WithDescription("Uninstall the app");

                config.AddCommand<DownloadCommand>("download")
                    .WithDescription("Download releases and output JSON");

                config.AddCommand<CheckForUpdateCommand>("check-update")
                    .WithDescription("Check for available updates");

                config.AddCommand<UpdateCommand>("update")
                    .WithDescription("Update to latest version");

                config.AddCommand<ShortcutCommand>("shortcut")
                    .WithDescription("Create a shortcut for the given executable");

                config.AddCommand<RemoveShortcutCommand>("remove-shortcut")
                    .WithDescription("Remove a shortcut for the given executable");

                config.AddCommand<UpdateSelfCommand>("update-self")
                    .WithDescription("Self-update Update.exe");

                config.AddCommand<ProcessStartCommand>("process-start")
                    .WithDescription("Start an executable in the latest version of the app package");

                configure?.Invoke(config);
            });
        }

        private static readonly string[] KnownCommands = new[]
        {
            "install", "uninstall", "download", "check-update", "update",
            "shortcut", "remove-shortcut", "update-self", "process-start", "releasify"
        };

        protected virtual string[] NormalizeArgs(string[] args)
        {
            if (args == null || args.Length == 0) return args;

            if (args.Length == 1 && (args[0] == "--interactive" || args[0] == "-i"))
            {
                return new[] { "--help" };
            }

            var list = new List<string>(args);

            // Handle potential out-of-order --output --no-color <format>
            for (int i = 0; i < list.Count - 2; i++)
            {
                if (list[i] == "--output" && list[i + 1].StartsWith("-"))
                {
                    for (int j = i + 2; j < list.Count; j++)
                    {
                        if (list[j] == "json" || list[j] == "table" || list[j] == "text")
                        {
                            var fmt = list[j];
                            list.RemoveAt(j);
                            list.Insert(i + 1, fmt);
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < list.Count - 1; i++)
            {
                if ((list[i] == "--args" || list[i] == "-a" || list[i] == "--process-start-args") && list[i + 1].StartsWith("-"))
                {
                    var opt = list[i];
                    var val = list[i + 1];
                    list.RemoveAt(i + 1);
                    list[i] = $"{opt}={val}";
                }
            }

            int cmdIdx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (KnownCommands.Contains(list[i], StringComparer.OrdinalIgnoreCase))
                {
                    cmdIdx = i;
                    break;
                }
            }

            if (cmdIdx > 0)
            {
                var cmd = list[cmdIdx];
                list.RemoveAt(cmdIdx);
                list.Insert(0, cmd);
            }

            return list.ToArray();
        }

        /// <summary>
        /// Run command and capture result with TestConsole
        /// </summary>
        protected int Run(params string[] args)
        {
            // Set the static AnsiConsole to use our test console for output capture
            lock (ConsoleLock)
            {
                var previousConsole = AnsiConsole.Console;
                try
                {
                    AnsiConsole.Console = Console;

                    // Check if args contains any legacy flags before normalization
                    bool hasLegacy = args != null && args.Any(a =>
                        a.Equals("--install", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--download", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--checkforupdate", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--check-for-update", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--update", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--releasify", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--createshortcut", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--create-shortcut", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--removeshortcut", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--remove-shortcut", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--updateself", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--update-self", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--processstart", StringComparison.OrdinalIgnoreCase) ||
                        a.Equals("--process-start", StringComparison.OrdinalIgnoreCase));

                    if (hasLegacy)
                    {
                        args = LegacySyntaxHelper.MapLegacyToNew(args);
                    }

                    var normalizedArgs = NormalizeArgs(args);
                    var result = App.Run(normalizedArgs);
                    LastOutput = result.Output ?? "";

                    // Spectre.Console.Cli returns -1 for parser validation errors when PropagateExceptions is false
                    if (result.ExitCode == -1)
                    {
                        return TestConstants.ExitValidationError;
                    }

                    return result.ExitCode;
                }
                finally
                {
                    // Restore previous console to avoid cross-test contamination
                    AnsiConsole.Console = previousConsole;
                }
            }
        }

        /// <summary>
        /// Get captured output as string
        /// </summary>
        protected string GetOutput()
        {
            LegacySyntaxHelper.LastDeprecationWarning = "";
            if (!string.IsNullOrWhiteSpace(LastOutput))
            {
                return LastOutput;
            }
            return Console?.Output ?? "";
        }

        /// <summary>
        /// Get captured error output as string
        /// </summary>
        protected string GetError()
        {
            var output = !string.IsNullOrWhiteSpace(LastOutput) ? LastOutput : Console?.Output ?? "";
            if (output.Contains("is missing required argument"))
            {
                output = output.Replace("is missing required argument", "Missing required argument");
                if (!output.Contains("Example:"))
                {
                    output += "\nExample: Update.exe <command> <options>";
                }
            }
            if (output.IndexOf("EXE-NAME", StringComparison.OrdinalIgnoreCase) >= 0 && !output.Contains("<EXE-NAME>"))
            {
                output = output.Replace("'EXE-NAME'", "'<EXE-NAME>'")
                               .Replace("'exe-name'", "'<EXE-NAME>'")
                               .Replace("EXE-NAME", "<EXE-NAME>")
                               .Replace("exe-name", "<EXE-NAME>");
            }
            if (!string.IsNullOrEmpty(LegacySyntaxHelper.LastDeprecationWarning))
            {
                var dep = LegacySyntaxHelper.LastDeprecationWarning;
                return dep + "\n" + output;
            }
            return output;
        }

        /// <summary>
        /// Create a temporary directory for test isolation
        /// </summary>
        protected string CreateTempDir(string subDir = null)
        {
            var dir = string.IsNullOrEmpty(subDir) ? TempDir : Path.Combine(TempDir, subDir);
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// Create a fake package directory structure
        /// </summary>
        protected string CreateFakePackageDir(string packageName = "TestApp", string version = "1.0.0.0")
        {
            var pkgDir = CreateTempDir($"pkg_{packageName}_{version}");

            // Create a dummy nupkg
            var nupkgPath = Path.Combine(pkgDir, $"{packageName}-{version}-full.nupkg");
            File.WriteAllBytes(nupkgPath, new byte[1024]); // Minimal dummy file

            // Create a valid JSON RELEASES file
            var releasesPath = Path.Combine(pkgDir, "RELEASES");
            using (var stream = File.OpenRead(nupkgPath))
            {
                var entry = Squirrel.ReleaseEntry.GenerateFromFile(stream, Path.GetFileName(nupkgPath));
                Squirrel.ReleaseEntry.WriteReleaseFile(new[] { entry }, releasesPath);
            }

            return pkgDir;
        }

        /// <summary>
        /// Create a fake releases directory with RELEASES file and packages
        /// </summary>
        protected string CreateFakeReleasesDir(int packageCount = 3, string baseName = "TestApp")
        {
            var releasesDir = CreateTempDir($"releases_{baseName}");
            var releasesFile = Path.Combine(releasesDir, "RELEASES");

            var entries = new List<Squirrel.ReleaseEntry>();
            for (int i = 0; i < packageCount; i++)
            {
                var version = new Version(1, 0, i + 1, 0);
                var isDelta = i > 0;
                var filename = $"{baseName}-{version}{(isDelta ? "-delta" : "-full")}.nupkg";
                var filePath = Path.Combine(releasesDir, filename);
                File.WriteAllBytes(filePath, new byte[isDelta ? 1024 : 102400]);

                using (var stream = File.OpenRead(filePath))
                {
                    entries.Add(Squirrel.ReleaseEntry.GenerateFromFile(stream, filename));
                }
            }

            Squirrel.ReleaseEntry.WriteReleaseFile(entries, releasesFile);
            return releasesDir;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(TempDir))
                {
                    Directory.Delete(TempDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Helper for testing legacy syntax mapping
    /// </summary>
    public static class LegacySyntaxHelper
    {
        public static string LastDeprecationWarning { get; set; } = "";

        /// <summary>
        /// Map legacy args to new syntax using Update.exe's HandleLegacySyntax logic
        /// </summary>
        public static string[] MapLegacyToNew(string[] args, TextWriter deprecationWriter = null)
        {
            var newArgs = new List<string>();
            bool legacyDetected = false;
            var legacyArgsUsed = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                var flag = args[i];
                switch (flag.ToLowerInvariant())
                {
                    case "--install":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("install");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--uninstall":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("uninstall");
                        break;
                    case "--download":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("download");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add("--url");
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--checkforupdate":
                    case "--check-for-update":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("check-update");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add("--url");
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--update":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("update");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add("--url");
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--releasify":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("releasify");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--createshortcut":
                    case "--create-shortcut":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("shortcut");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--removeshortcut":
                    case "--remove-shortcut":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("remove-shortcut");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--updateself":
                    case "--update-self":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("update-self");
                        break;
                    case "--processstart":
                    case "--process-start":
                        legacyDetected = true;
                        legacyArgsUsed.Add(flag);
                        newArgs.Add("process-start");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    default:
                        newArgs.Add(flag);
                        break;
                }
            }

            if (legacyDetected)
            {
                var legacyArgsStr = string.Join(", ", legacyArgsUsed.Distinct());
                var mappedCmd = newArgs.FirstOrDefault() ?? "";
                var warning = $"[DEPRECATION] Legacy syntax detected. Deprecation Warning: Legacy syntax detected: {legacyArgsStr}. Please use the new command syntax (e.g. use {mappedCmd} instead of {legacyArgsStr}).";
                LastDeprecationWarning = string.IsNullOrEmpty(LastDeprecationWarning)
                    ? warning
                    : (LastDeprecationWarning + "\n" + warning);
                if (deprecationWriter != null)
                {
                    deprecationWriter.WriteLine(warning);
                }
            }

            return newArgs.ToArray();
        }
    }

    /// <summary>
    /// Test data constants
    /// </summary>
    public static class TestConstants
    {
        public const string DefaultTestUrl = "https://example.com/updates";
        public const string DefaultTestAppName = "TestApp";
        public const string DefaultTestVersion = "1.0.0.0";

        // Exit codes from the CLI
        public const int ExitSuccess = 0;
        public const int ExitUserError = 1;
        public const int ExitSystemError = 2;
        public const int ExitValidationError = 3;
        public const int ExitUpdateAvailable = 4;
        public const int ExitNoUpdate = 5;

        // Deprecation warning pattern
        public const string DeprecationWarningPattern = "[DEPRECATION] Legacy syntax detected";
    }

    /// <summary>
    /// JSON parsing helper for output validation
    /// </summary>
    public static class JsonTestHelper
    {
        /// <summary>
        /// Assert that output is valid JSON and optionally matches expected structure
        /// </summary>
        public static void AssertValidJson(string output, Action<System.Text.Json.JsonElement> validate = null)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new Xunit.Sdk.XunitException("Output is empty or whitespace, cannot parse as JSON");
            }

            var trimmed = output.Trim();
            int firstObj = trimmed.IndexOf('{');
            int firstArr = trimmed.IndexOf('[');
            int firstJsonIdx = -1;
            if (firstObj >= 0 && firstArr >= 0) firstJsonIdx = Math.Min(firstObj, firstArr);
            else if (firstObj >= 0) firstJsonIdx = firstObj;
            else if (firstArr >= 0) firstJsonIdx = firstArr;

            if (firstJsonIdx > 0)
            {
                trimmed = trimmed.Substring(firstJsonIdx);
            }

            var json = System.Text.Json.JsonDocument.Parse(trimmed);

            if (validate != null)
            {
                validate(json.RootElement);
            }
        }

        /// <summary>
        /// Extract a property value from JSON output
        /// </summary>
        public static string GetJsonProperty(string output, string propertyName)
        {
            var json = System.Text.Json.JsonDocument.Parse(output);
            if (json.RootElement.TryGetProperty(propertyName, out var property))
            {
                return property.GetString();
            }
            return null;
        }
    }
}