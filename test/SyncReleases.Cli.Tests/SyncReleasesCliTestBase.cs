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
    /// Base class for SyncReleases CLI command tests using Spectre.Console.Testing
    /// </summary>
    public abstract class SyncReleasesCliTestBase : IDisposable
    {
        private static readonly object ConsoleLock = new object();

        protected readonly TestConsole Console;
        protected CommandAppTester App;
        protected readonly string TempDir;
        protected readonly StringBuilder ErrorOutput;
        protected string LastOutput = "";

        protected SyncReleasesCliTestBase()
        {
            Console = new TestConsole();
            ErrorOutput = new StringBuilder();
            TempDir = Path.Combine(Path.GetTempPath(), $"SquirrelSyncReleasesCliTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(TempDir);

            ConfigureSyncReleasesApp();
        }

        protected void ConfigureSyncReleasesApp(Action<IConfigurator> configure = null)
        {
            App = new CommandAppTester();
            App.Configure(config =>
            {
                config.SetApplicationName("SyncReleases.exe");
                config.ValidateExamples();

                config.AddCommand<SyncCommand>("sync")
                    .WithDescription("Sync releases from GitHub or remote RELEASES folder");

                config.AddCommand<ValidateCommand>("validate")
                    .WithDescription("Validate a releases directory");

                config.AddCommand<ListCommand>("list")
                    .WithDescription("List releases in a directory");

                configure?.Invoke(config);
            });
        }

        private static readonly string[] KnownCommands = new[] { "sync", "validate", "list" };

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
            if (!string.IsNullOrWhiteSpace(LastOutput))
            {
                return LastOutput;
            }
            return Console?.Output ?? "";
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
    /// Test data constants
    /// </summary>
    public static class TestConstants
    {
        public const string DefaultTestUrl = "https://github.com/test/repo";
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