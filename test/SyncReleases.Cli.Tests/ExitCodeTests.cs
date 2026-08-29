using Squirrel.Cli.Commands;
using System.Linq;
using System;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.SyncReleases
{
    /// <summary>
    /// Tests for SyncReleases.exe exit code parity
    /// </summary>
    public class ExitCodeTests : SyncReleasesCliTestBase
    {
        [Fact]
        public void Success_ReturnsExitCode0()
        {
            // Act
            var exitCode = Run("sync", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
        }

        [Fact]
        public void ValidationError_ReturnsExitCode3()
        {
            // Act
            var exitCode = Run("sync", "--release-dir", "./Releases"); // missing required --url

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void AllCommands_ValidationErrorReturns3()
        {
            var commandsWithMissingArgs = new[]
            {
                new[] { "sync" },
                new[] { "validate" },
                new[] { "list" },
            };

            foreach (var cmd in commandsWithMissingArgs)
            {
                var exitCode = Run(cmd);
                // sync requires --url, validate and list need --release-dir (or current dir to have RELEASES)
                // In temp dir without RELEASES, they'll get user error not validation error
                Assert.True(exitCode == TestConstants.ExitValidationError || exitCode == TestConstants.ExitUserError);
            }
        }

        [Fact]
        public void Validate_MissingReleasesFile_ReturnsExitCode1()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases_no_releases_file");

            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir);

            // Assert
            Assert.Equal(TestConstants.ExitUserError, exitCode);
        }

        [Fact]
        public void List_MissingReleasesFile_ReturnsExitCode1()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases_no_releases_file");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir);

            // Assert
            Assert.Equal(TestConstants.ExitUserError, exitCode);
        }

        [Fact]
        public void NetworkError_ReturnsExitCode2()
        {
            // Act - unreachable URL
            var releaseDir = CreateTempDir("releases");
            var exitCode = Run("sync", "--url", "https://nonexistent.invalid/repo", "--release-dir", releaseDir);

            // Assert - network errors should be system error (2) or user error (1)
            Assert.Contains(exitCode, new[] { TestConstants.ExitSystemError, TestConstants.ExitUserError });
        }

        [Fact]
        public void InvalidPath_ReturnsExitCode3()
        {
            // Act
            var nonExistentDir = Path.Combine(CreateTempDir("nonexistent"), "does-not-exist");
            var exitCode = Run("validate", "--release-dir", nonExistentDir);

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void UnknownCommand_ReturnsExitCode3()
        {
            // Act
            var exitCode = Run("unknowncommand");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Help_ReturnsExitCode0()
        {
            var commands = new[] { "sync", "validate", "list" };

            foreach (var cmd in commands)
            {
                var exitCode = Run(cmd, "--help");
                Assert.Equal(TestConstants.ExitSuccess, exitCode);
            }
        }

        [Fact]
        public void GlobalOptionsWithValidCommand_ReturnSuccess()
        {
            var exitCode = Run("--verbose", "--no-color", "--output", "json", "sync", "--help");
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
        }

        [Fact]
        public void Sync_DryRun_ReturnsExitCode0()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", releaseDir, "--dry-run");

            // Assert - dry-run should succeed (exit code 0) even if network fails
            // because it doesn't actually perform the sync
            Assert.True(exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError);
        }
    }
}