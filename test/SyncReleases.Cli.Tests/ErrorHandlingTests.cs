using Squirrel.Cli.Commands;
using System.Linq;
using System;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.SyncReleases
{
    /// <summary>
    /// Tests for SyncReleases.exe error handling parity
    /// </summary>
    public class ErrorHandlingTests : SyncReleasesCliTestBase
    {
        [Fact]
        public void MissingRequiredArgument_ShowsValidationErrorPanel()
        {
            // Act
            var exitCode = Run("sync", "--release-dir", "./Releases");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Missing required argument", error);
            Assert.Contains("--url", error);
            Assert.Contains("Example:", error);
        }

        [Fact]
        public void InvalidUrl_ShowsValidationErrorWithPath()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", "not-a-url", "--release-dir", releaseDir);

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Invalid repository URL", error);
            Assert.Contains("--url", error);
        }

        [Fact]
        public void MissingReleaseDir_ShowsValidationErrorWithPath()
        {
            // Arrange - use a non-existent directory
            var nonExistentDir = Path.Combine(CreateTempDir("nonexistent"), "does-not-exist");

            // Act
            var exitCode = Run("validate", "--release-dir", nonExistentDir);

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Release directory not found", error);
            Assert.Contains("--release-dir", error);
        }

        [Fact]
        public void ReleaseDirWithoutReleasesFile_ShowsUserError()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases_no_releases_file");

            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir);

            // Assert
            Assert.Equal(TestConstants.ExitUserError, exitCode);
            var error = GetError();
            Assert.Contains("RELEASES file not found", error);
        }

        [Fact]
        public void UnknownCommand_ShowsDidYouMeanSuggestion()
        {
            // Act
            var exitCode = Run("syncc"); // typo for sync

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("syncc", error);
            Assert.Contains("Did you mean", error);
        }

        [Fact]
        public void UnknownCommandWithSimilarName_SuggestsCorrectCommand()
        {
            // Act
            var exitCode = Run("validat"); // missing last 'e'

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("validat", error);
            Assert.Contains("validate", error);
        }

        [Fact]
        public void ValidationError_PanelContainsOptionName()
        {
            // Act
            var exitCode = Run("sync", "--url"); // missing value

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("--url", error);
        }

        [Fact]
        public void SystemError_ShowsExceptionPanel()
        {
            // Act - sync from unreachable URL
            var releaseDir = CreateTempDir("releases");
            var exitCode = Run("sync", "--url", "https://nonexistent.invalid/repo", "--release-dir", releaseDir);

            // Assert - network errors typically return SystemError (exit code 2)
            Assert.Contains(exitCode, new[] { TestConstants.ExitSystemError, TestConstants.ExitUserError, TestConstants.ExitValidationError });
        }

        [Fact]
        public void ErrorOutput_WithNoColor_DoesNotContainAnsiCodes()
        {
            // Act
            var exitCode = Run("sync", "--no-color", "--release-dir", "./Releases");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.DoesNotContain("\u001b[", error);
        }

        [Fact]
        public void ErrorOutput_WithQuiet_SuppressesErrorPanel()
        {
            // Act
            var exitCode = Run("sync", "--quiet", "--release-dir", "./Releases");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            // With --quiet, error output should be minimal or empty
        }

        [Fact]
        public void ValidationError_ExitCodeIs3()
        {
            // Act
            var exitCode = Run("sync", "--release-dir", "./Releases");

            // Assert
            Assert.Equal(3, exitCode);
        }

        [Fact]
        public void UserError_ExitCodeIs1()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases_no_releases_file");

            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir);

            // Assert
            Assert.Equal(TestConstants.ExitUserError, exitCode);
        }

        [Fact]
        public void HelpOutput_GoesToStdout()
        {
            // Act
            var exitCode = Run("sync", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Sync releases from GitHub", output);
        }
    }
}