using Squirrel.Cli.Commands;
using System.Linq;
using System;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.SyncReleases
{
    /// <summary>
    /// Tests for SyncReleases.exe sync command parity
    /// </summary>
    public class SyncCommandTests : SyncReleasesCliTestBase
    {
        [Fact]
        public void Sync_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("sync", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Sync releases from GitHub or remote RELEASES folder", output);
            Assert.Contains("--release-dir", output);
            Assert.Contains("--url", output);
            Assert.Contains("--token", output);
            Assert.Contains("--dry-run", output);
            Assert.Contains("--parallel", output);
        }

        [Fact]
        public void Sync_MissingUrl_ReturnsValidationError()
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
        public void Sync_InvalidUrl_ReturnsValidationError()
        {
            // Act
            var exitCode = Run("sync", "--url", "not-a-url", "--release-dir", "./Releases");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Invalid repository URL", error);
            Assert.Contains("--url", error);
        }

        [Fact]
        public void Sync_WithUrlAndReleaseDir_RunsWithoutValidationError()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", releaseDir);

            // Assert - should not be validation error (may be system error due to network)
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Sync_WithDryRun_ShowsConfirmationPanel()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", releaseDir, "--dry-run");

            // Assert
            var output = GetOutput();
            // Dry-run should show what would be done
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("dry-run", output.ToLower());
            }
        }

        [Fact]
        public void Sync_WithParallel_ParsesCorrectly()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", releaseDir, "--parallel", "8");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Sync_WithToken_ParsesCorrectly()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", releaseDir, "--token", "ghp_test");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Sync_DefaultReleaseDir_UsesCurrentDirectory()
        {
            // Act - no --release-dir provided
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl);

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Sync_OutputJson_ProducesValidJson()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", releaseDir, "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void Sync_OutputTable_ProducesTableOutput()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", releaseDir, "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void Sync_OutputText_ProducesTextOutput()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", releaseDir, "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}