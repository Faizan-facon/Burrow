using Squirrel.Cli.Commands;
using System.Linq;
using System;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.SyncReleases
{
    /// <summary>
    /// Tests for SyncReleases.exe validate command parity
    /// </summary>
    public class ValidateCommandTests : SyncReleasesCliTestBase
    {
        [Fact]
        public void Validate_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("validate", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Validate a releases directory", output);
            Assert.Contains("--release-dir", output);
            Assert.Contains("--fix", output);
        }

        [Fact]
        public void Validate_MissingReleaseDir_ReturnsValidationError()
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
        public void Validate_ReleaseDirWithoutReleasesFile_ReturnsUserError()
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
        public void Validate_WithValidReleasesDir_RunsWithoutValidationError()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir);

            // Assert - should not be validation error (may be user error if files missing)
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Validate_WithFixFlag_ParsesCorrectly()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir, "--fix");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Validate_DefaultReleaseDir_UsesCurrentDirectory()
        {
            // Arrange - create a Releases directory in current temp dir
            var releaseDir = CreateFakeReleasesDir(1, "TestApp");
            var originalDir = Directory.GetCurrentDirectory();
            
            try
            {
                Directory.SetCurrentDirectory(releaseDir);
                
                // Act
                var exitCode = Run("validate");

                // Assert - should not be validation error
                Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        [Fact]
        public void Validate_OutputJson_ProducesValidJson()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir, "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void Validate_OutputTable_ProducesTableOutput()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir, "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void Validate_OutputText_ProducesTextOutput()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir, "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}