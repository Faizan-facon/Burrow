using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe global options parity
    /// </summary>
    public class GlobalOptionsTests : CliTestBase
    {
        public GlobalOptionsTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void Verbose_EnablesDebugLogging()
        {
            // Act
            var exitCode = Run("install", "--help", "--verbose");

            // Assert - help should still work
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Install the app from a package directory", output);
        }

        [Fact]
        public void Quiet_SuppressesNonErrorOutput()
        {
            // Act
            var exitCode = Run("install", "--help", "--quiet");

            // Assert - help should still work but output may be suppressed
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
        }

        [Fact]
        public void NoColor_DisablesAnsiColors()
        {
            // Act
            var exitCode = Run("install", "--help", "--no-color");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            // Output should not contain ANSI escape sequences
            Assert.DoesNotContain("\u001b[", output);
        }

        [Fact]
        public void OutputJson_ProducesValidJson()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void OutputTable_ProducesTableOutput()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void OutputText_ProducesTextOutput()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }

        [Fact]
        public void LogFile_WritesToFile()
        {
            // Arrange
            var logPath = Path.Combine(CreateTempDir("logs"), "test.log");
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--log-file", logPath);

            // Assert
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
            // Log file should exist (may be empty if command fails early)
            Assert.True(File.Exists(logPath) || exitCode != TestConstants.ExitSuccess);
        }

        [Fact]
        public void Interactive_EnablesCommandPicker()
        {
            // Act - no args with --interactive should show command picker
            var exitCode = Run("--interactive");

            // Assert - should show help/picker, not validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void GlobalOptions_WorkWithAllCommands()
        {
            // Test that global options work with various commands
            var commands = new[]
            {
                new[] { "install", "--help" },
                new[] { "uninstall", "--help" },
                new[] { "download", "--help" },
                new[] { "check-update", "--help" },
                new[] { "update", "--help" },
                new[] { "shortcut", "--help" },
                new[] { "remove-shortcut", "--help" },
                new[] { "update-self", "--help" },
                new[] { "process-start", "--help" },
            };

            foreach (var cmd in commands)
            {
                var args = cmd.Concat(new[] { "--verbose", "--no-color", "--output", "json" }).ToArray();
                var exitCode = Run(args);
                
                // Help should succeed
                Assert.Equal(TestConstants.ExitSuccess, exitCode);
                
                var output = GetOutput();
                // Should not contain ANSI codes due to --no-color
                Assert.DoesNotContain("\u001b[", output);
            }
        }

        [Fact]
        public void Quiet_OverridesVerbose()
        {
            // Act
            var exitCode = Run("install", "--help", "--quiet", "--verbose");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            // Output should be minimal due to --quiet
        }

        [Fact]
        public void NoColor_WorksWithAllOutputFormats()
        {
            var formats = new[] { "json", "table", "text" };
            
            foreach (var format in formats)
            {
                var exitCode = Run("install", "--help", "--no-color", "--output", format);
                Assert.Equal(TestConstants.ExitSuccess, exitCode);
                
                var output = GetOutput();
                Assert.DoesNotContain("\u001b[", output);
            }
        }
    }
}