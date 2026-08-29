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
        protected readonly TestConsole Console;
        protected readonly CommandApp App;
        protected readonly string TempDir;
        protected readonly StringBuilder ErrorOutput;

        protected CliTestBase()
        {
            Console = new TestConsole();
            ErrorOutput = new StringBuilder();
            TempDir = Path.Combine(Path.GetTempPath(), $"SquirrelCliTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(TempDir);

            App = new CommandApp();
        }

        protected void ConfigureUpdateApp(Action<IConfigurator> configure = null)
        {
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
        /// Create a fake package directory structure
        /// </summary>
        protected string CreateFakePackageDir(string packageName = "TestApp", string version = "1.0.0.0")
        {
            var pkgDir = CreateTempDir($"pkg_{packageName}_{version}");
            
            // Create a dummy RELEASES file
            var releasesPath = Path.Combine(pkgDir, "RELEASES");
            File.WriteAllText(releasesPath, $"{packageName} {version} {packageName}-{version}-full.nupkg {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} 0 SHA256:dummyhash");
            
            // Create a dummy nupkg
            var nupkgPath = Path.Combine(pkgDir, $"{packageName}-{version}-full.nupkg");
            File.WriteAllBytes(nupkgPath, new byte[1024]); // Minimal dummy file
            
            return pkgDir;
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
    /// Helper for testing legacy syntax mapping
    /// </summary>
    public static class LegacySyntaxHelper
    {
        /// <summary>
        /// Map legacy args to new syntax using Update.exe's HandleLegacySyntax logic
        /// </summary>
        public static string[] MapLegacyToNew(string[] args)
        {
            var newArgs = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--install":
                        newArgs.Add("install");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--uninstall":
                        newArgs.Add("uninstall");
                        break;
                    case "--download":
                        newArgs.Add("download");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add("--url");
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--checkforupdate":
                    case "--check-for-update":
                        newArgs.Add("check-update");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add("--url");
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--update":
                        newArgs.Add("update");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add("--url");
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--releasify":
                        newArgs.Add("releasify");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--createshortcut":
                    case "--create-shortcut":
                        newArgs.Add("shortcut");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--removeshortcut":
                    case "--remove-shortcut":
                        newArgs.Add("remove-shortcut");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--updateself":
                    case "--update-self":
                        newArgs.Add("update-self");
                        break;
                    case "--processstart":
                    case "--process-start":
                        newArgs.Add("process-start");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    default:
                        newArgs.Add(args[i]);
                        break;
                }
            }

            return newArgs.ToArray();
        }
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
}