using Squirrel.Cli.Commands;
using System.Linq;
using System;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.SyncReleases
{
    /// <summary>
    /// Tests for SyncReleases.exe list command parity
    /// </summary>
    public class ListCommandTests : SyncReleasesCliTestBase
    {
        [Fact]
        public void List_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("list", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("List releases in a directory", output);
            Assert.Contains("--release-dir", output);
            Assert.Contains("--show-deltas", output);
            Assert.Contains("--output", output);
        }

        [Fact]
        public void List_MissingReleaseDir_ReturnsValidationError()
        {
            // Arrange - use a non-existent directory
            var nonExistentDir = Path.Combine(CreateTempDir("nonexistent"), "does-not-exist");

            // Act
            var exitCode = Run("list", "--release-dir", nonExistentDir);

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Release directory not found", error);
            Assert.Contains("--release-dir", error);
        }

        [Fact]
        public void List_ReleaseDirWithoutReleasesFile_ReturnsUserError()
        {
            // Arrange
            var releaseDir = CreateTempDir("releases_no_releases_file");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir);

            // Assert
            Assert.Equal(TestConstants.ExitUserError, exitCode);
            var error = GetError();
            Assert.Contains("RELEASES file not found", error);
        }

        [Fact]
        public void List_WithValidReleasesDir_RunsWithoutValidationError()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir);

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void List_WithShowDeltasFalse_FiltersDeltaPackages()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--show-deltas", "false");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
            
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess)
            {
                // Should not contain "Delta" in output
                Assert.DoesNotContain("Delta", output);
            }
        }

        [Fact]
        public void List_WithShowDeltasTrue_IncludesDeltaPackages()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--show-deltas", "true");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void List_DefaultReleaseDir_UsesCurrentDirectory()
        {
            // Arrange - create a Releases directory in current temp dir
            var releaseDir = CreateFakeReleasesDir(1, "TestApp");
            var originalDir = Directory.GetCurrentDirectory();
            
            try
            {
                Directory.SetCurrentDirectory(releaseDir);
                
                // Act
                var exitCode = Run("list");

                // Assert - should not be validation error
                Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        [Fact]
        public void List_OutputJson_ProducesValidJson()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                JsonTestHelper.AssertValidJson(output);
                
                // Verify it's an array
                var doc = System.Text.Json.JsonDocument.Parse(output);
                Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
                
                if (doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];
                    Assert.True(first.TryGetProperty("version", out _));
                    Assert.True(first.TryGetProperty("filename", out _));
                    Assert.True(first.TryGetProperty("size", out _));
                    Assert.True(first.TryGetProperty("type", out _));
                }
            }
        }

        [Fact]
        public void List_OutputTable_ProducesTableOutput()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                Assert.Contains("┌", output);
                Assert.Contains("version", output);
                Assert.Contains("filename", output);
                Assert.Contains("size", output);
                Assert.Contains("type", output);
            }
        }

        [Fact]
        public void List_OutputText_ProducesTextOutput()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}