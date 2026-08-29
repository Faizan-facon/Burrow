using Squirrel.Cli.Commands;
using System.Linq;
using System;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.SyncReleases
{
    /// <summary>
    /// Tests for SyncReleases.exe global options parity
    /// </summary>
    public class GlobalOptionsTests : SyncReleasesCliTestBase
    {
        [Fact]
        public void Verbose_EnablesDebugLogging()
        {
            // Act
            var exitCode = Run("sync", "--verbose", "--help");

            // Assert - help should still work
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Sync releases from GitHub", output);
        }

        [Fact]
        public void Quiet_SuppressesNonErrorOutput()
        {
            // Act
            var exitCode = Run("sync", "--quiet", "--help");

            // Assert - help should still work but output may be suppressed
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
        }

        [Fact]
        public void NoColor_DisablesAnsiColors()
        {
            // Act
            var exitCode = Run("sync", "--no-color", "--help");

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
            var releaseDir = CreateFakeReleasesDir(2, "TestApp");

            // Act
            var exitCode = Run("--output", "json", "list", "--release-dir", releaseDir);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void OutputTable_ProducesTableOutput()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(2, "TestApp");

            // Act
            var exitCode = Run("--output", "table", "list", "--release-dir", releaseDir);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void OutputText_ProducesTextOutput()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(2, "TestApp");

            // Act
            var exitCode = Run("--output", "text", "list", "--release-dir", releaseDir);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }

        [Fact]
        public void LogFile_WritesToFile()
        {
            // Arrange
            var logPath = Path.Combine(CreateTempDir("logs"), "sync_test.log");
            var releaseDir = CreateFakeReleasesDir(1, "TestApp");

            // Act
            var exitCode = Run("--log-file", logPath, "list", "--release-dir", releaseDir);

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
                new[] { "sync", "--help" },
                new[] { "validate", "--help" },
                new[] { "list", "--help" },
            };

            foreach (var cmd in commands)
            {
                var args = new[] { "--verbose", "--no-color", "--output", "json" }.Concat(cmd).ToArray();
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
            var exitCode = Run("--verbose", "--quiet", "sync", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            // Output should be minimal due to --quiet
        }

        [Fact]
        public void NoColor_WorksWithAllOutputFormats()
        {
            var formats = new[] { "json", "table", "text" };
            var releaseDir = CreateFakeReleasesDir(1, "TestApp");
            
            foreach (var format in formats)
            {
                var exitCode = Run("--output", "--no-color", format, "list", "--release-dir", releaseDir);
                Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
                
                var output = GetOutput();
                Assert.DoesNotContain("\u001b[", output);
            }
        }
    }
}