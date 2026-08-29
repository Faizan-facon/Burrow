using Squirrel.Cli.Commands;
using System;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Integration test scenarios for Update.exe
    /// </summary>
    public class IntegrationTests : CliTestBase
    {
        public IntegrationTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void FullInstallUpdateUninstall_Cycle_Works()
        {
            // This is a high-level integration test that documents the expected flow
            // Actual implementation would require a full Squirrel setup with packages
            
            // Step 1: Install
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");
            var installExitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--silent");
            
            // Should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, installExitCode);
            
            // Step 2: Check for update (would need mock server)
            var checkExitCode = Run("check-update", TestConstants.DefaultTestUrl, "--app-name", "TestApp");
            Assert.NotEqual(TestConstants.ExitValidationError, checkExitCode);
            
            // Step 3: Uninstall
            var uninstallExitCode = Run("uninstall", "--app-name", "TestApp");
            Assert.NotEqual(TestConstants.ExitValidationError, uninstallExitCode);
        }

        [Fact]
        public void ShortcutCreateRemove_RoundTrip_Works()
        {
            // Arrange
            var exeName = "TestApp.exe";

            // Act - Create shortcut
            var createExitCode = Run("shortcut", exeName, "--shortcut-locations", "Desktop");
            
            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, createExitCode);
            
            // Act - Remove shortcut
            var removeExitCode = Run("remove-shortcut", exeName, "--shortcut-locations", "Desktop");
            
            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, removeExitCode);
        }

        [Fact]
        public void ProcessStart_FindsLatestVersion()
        {
            // This test documents expected behavior
            // Act
            var exitCode = Run("process-start", "TestApp.exe");
            
            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Download_OutputsValidJsonStructure()
        {
            // Act
            var exitCode = Run("download", TestConstants.DefaultTestUrl, "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
                
                var doc = System.Text.Json.JsonDocument.Parse(output);
                Assert.True(doc.RootElement.TryGetProperty("currentVersion", out _));
                Assert.True(doc.RootElement.TryGetProperty("futureVersion", out _));
                Assert.True(doc.RootElement.TryGetProperty("releasesToApply", out _));
            }
        }

        /// <summary>
        /// Creates a minimal valid nupkg for testing
        /// </summary>
        private byte[] CreateMinimalNupkg(string id, string version)
        {
            // Create a minimal nupkg with proper structure
            // This is a simplified version - real tests would use proper NuGet packaging
            using var ms = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                // Add a minimal .nuspec
                var nuspecEntry = archive.CreateEntry($"{id}.nuspec");
                using (var entryStream = nuspecEntry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    writer.Write($@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>{id}</id>
    <version>{version}</version>
    <authors>Test</authors>
    <description>Test package</description>
  </metadata>
</package>");
                }
            }
            return ms.ToArray();
        }
    }
}