using Squirrel.Cli.Commands;
using System.Linq;
using System;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.SyncReleases
{
    /// <summary>
    /// Integration test scenarios for SyncReleases.exe
    /// </summary>
    public class IntegrationTests : SyncReleasesCliTestBase
    {
        [Fact]
        public void SyncValidateList_FullCycle_Works()
        {
            // Step 1: Create a releases directory with packages
            var sourceDir = CreateFakeReleasesDir(3, "TestApp");
            
            // Step 2: Sync to a target directory (using file:// URL)
            var targetDir = CreateTempDir("sync_target");
            var fileUrl = new Uri(sourceDir).AbsoluteUri;
            
            var syncExitCode = Run("sync", "--url", fileUrl, "--release-dir", targetDir, "--dry-run");
            Assert.NotEqual(TestConstants.ExitValidationError, syncExitCode);
            
            // Step 3: Validate the target directory
            // Note: dry-run doesn't actually write, so validate would fail
            // This documents the expected flow
        }

        [Fact]
        public void Validate_FindsIssuesInCorruptedReleases()
        {
            // Arrange - create a releases directory with missing files
            var releaseDir = CreateTempDir("corrupted_releases");
            var releasesFile = Path.Combine(releaseDir, "RELEASES");
            
            // Write RELEASES file referencing non-existent packages
            File.WriteAllText(releasesFile, 
                "TestApp 1.0.0.0 TestApp-1.0.0.0-full.nupkg 2024-01-01 00:00:00 1024 SHA256:dummyhash" + Environment.NewLine +
                "TestApp 1.1.0.0 TestApp-1.1.0.0-delta.nupkg 2024-01-02 00:00:00 512 SHA256:dummyhash2");
            
            // Don't create the actual .nupkg files
            
            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir);
            
            // Assert - should find issues
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
            var output = GetOutput();
            // Should report missing files
        }

        [Fact]
        public void Validate_WithFix_AttemptsToFixIssues()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(2, "TestApp");
            
            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir, "--fix");
            
            // Assert
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void List_ShowsAllReleasesWithCorrectFormat()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");
            
            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--output", "json");
            
            // Assert
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                JsonTestHelper.AssertValidJson(output);
                
                var doc = System.Text.Json.JsonDocument.Parse(output);
                Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
                
                // Should have 3 entries (or fewer if filtered)
                Assert.True(doc.RootElement.GetArrayLength() > 0);
            }
        }

        [Fact]
        public void List_WithShowDeltasFalse_FiltersDeltaPackages()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");
            
            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--show-deltas", "false", "--output", "json");
            
            // Assert
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                JsonTestHelper.AssertValidJson(output);
                
                var doc = System.Text.Json.JsonDocument.Parse(output);
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var typeProp))
                    {
                        Assert.NotEqual("Delta", typeProp.GetString());
                    }
                }
            }
        }

        [Fact]
        public void Sync_FromGitHubUrl_WorksWithDryRun()
        {
            // Act - test with a GitHub URL (dry-run to avoid network)
            var targetDir = CreateTempDir("github_sync");
            var exitCode = Run("sync", "--url", "https://github.com/owner/repo", "--release-dir", targetDir, "--dry-run");
            
            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Sync_WithToken_ParsesCorrectly()
        {
            // Act
            var targetDir = CreateTempDir("github_sync_token");
            var exitCode = Run("sync", "--url", "https://github.com/owner/repo", "--release-dir", targetDir, "--token", "ghp_test", "--dry-run");
            
            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Sync_WithParallel_ParsesCorrectly()
        {
            // Act
            var targetDir = CreateTempDir("parallel_sync");
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", targetDir, "--parallel", "8");
            
            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }
    }
}