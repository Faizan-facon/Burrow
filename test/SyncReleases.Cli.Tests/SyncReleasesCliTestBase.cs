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
        protected readonly TestConsole Console;
        protected readonly CommandApp App;
        protected readonly string TempDir;
        protected readonly StringBuilder ErrorOutput;

        protected SyncReleasesCliTestBase()
        {
            Console = new TestConsole();
            ErrorOutput = new StringBuilder();
            TempDir = Path.Combine(Path.GetTempPath(), $"SquirrelSyncTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(TempDir);

            App = new CommandApp();
            ConfigureSyncReleasesApp();
        }

        protected void ConfigureSyncReleasesApp(Action<IConfigurator> configure = null)
        {
            App.Configure(config =>
            {
                config.SetApplicationName("SyncReleases.exe");
                config.ValidateExamples();

                config.AddCommand<SyncCommand>("sync")
                    .WithDescription("Sync releases from GitHub or remote RELEASES folder")
                    .WithExample(new[] { "sync", "--url", "https://github.com/owner/repo", "--release-dir", "./Releases" })
                    .WithExample(new[] { "sync", "--url", "https://github.com/owner/repo", "--token", "ghp_xxx", "--dry-run" })
                    .WithExample(new[] { "sync", "--url", "https://example.com/Releases", "--parallel", "8" });

                config.AddCommand<ValidateCommand>("validate")
                    .WithDescription("Validate a releases directory")
                    .WithExample(new[] { "validate", "--release-dir", "./Releases" })
                    .WithExample(new[] { "validate", "--release-dir", "./Releases", "--fix" });

                config.AddCommand<ListCommand>("list")
                    .WithDescription("List releases in a directory")
                    .WithExample(new[] { "list", "--release-dir", "./Releases" })
                    .WithExample(new[] { "list", "--release-dir", "./Releases", "--output", "json" });

                configure?.Invoke(config);
            });
        }

        /// <summary>
        /// Run command and capture result with TestConsole
        /// </summary>
        protected int Run(params string[] args)
        {
            var exitCode = App.Run(args);
            
            // Spectre.Console.Cli returns -1 for parser validation errors when PropagateExceptions is false
            if (exitCode == -1)
            {
                return TestConstants.ExitValidationError;
            }
            
            return exitCode;
        }

        /// <summary>
        /// Get captured output as string
        /// </summary>
        protected string GetOutput()
        {
            return Console.Output;
        }

        /// <summary>
        /// Get captured error output as string
        /// </summary>
        protected string GetError()
        {
            return Console.Output; // TestConsole captures both stdout and stderr in Output
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

            var lines = new List<string>();
            for (int i = 0; i < packageCount; i++)
            {
                var version = new Version(1, 0, i + 1, 0);
                var isDelta = i > 0;
                var filename = $"{baseName}-{version}{(isDelta ? "-delta" : "-full")}.nupkg";
                var line = $"{baseName} {version} {filename} {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} {(isDelta ? 1024 : 102400)} SHA256:dummyhash{i}";
                lines.Add(line);

                // Create dummy package file
                File.WriteAllBytes(Path.Combine(releasesDir, filename), new byte[isDelta ? 1024 : 102400]);
            }

            File.WriteAllText(releasesFile, string.Join(Environment.NewLine, lines));
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
            var doc = System.Text.Json.JsonDocument.Parse(output);
            validate?.Invoke(doc.RootElement);
        }

        /// <summary>
        /// Extract a property value from JSON output
        /// </summary>
        public static string GetJsonProperty(string output, string propertyName)
        {
            var doc = System.Text.Json.JsonDocument.Parse(output);
            if (doc.RootElement.TryGetProperty(propertyName, out var prop))
            {
                return prop.GetString();
            }
            return null;
        }
    }
}