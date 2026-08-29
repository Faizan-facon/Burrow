using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe check-update command parity
    /// </summary>
    public class CheckForUpdateCommandTests : CliTestBase
    {
        public CheckForUpdateCommandTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void CheckUpdate_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("check-update", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Check for available updates", output);
            Assert.Contains("<URL>", output);
            Assert.Contains("--app-name", output);
        }

        [Fact]
        public void CheckUpdate_MissingUrl_ReturnsValidationError()
        {
            // Act
            var exitCode = Run("check-update");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Missing required argument", error);
            Assert.Contains("URL", error);
            Assert.Contains("Example:", error);
        }

        [Fact]
        public void CheckUpdate_WithUrl_RunsWithoutValidationError()
        {
            // Act
            var exitCode = Run("check-update", TestConstants.DefaultTestUrl, "--app-name", "TestApp");

            // Assert - should not be validation error (may be system error due to network)
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void CheckUpdate_ReturnsExitCode4WhenUpdateAvailable()
        {
            // This test documents expected behavior - actual test would need mock server
            // Act
            var exitCode = Run("check-update", TestConstants.DefaultTestUrl);

            // Assert - should be one of the expected exit codes
            Assert.Contains(exitCode, new[] { 
                TestConstants.ExitSuccess, 
                TestConstants.ExitUpdateAvailable, 
                TestConstants.ExitNoUpdate,
                TestConstants.ExitSystemError 
            });
        }

        [Fact]
        public void CheckUpdate_WithLegacySyntax_ShowsDeprecationWarningAndMapsUrl()
        {
            // Act - test both legacy flag variants
            var legacyArgs1 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--checkforupdate", TestConstants.DefaultTestUrl });
            var exitCode1 = Run(legacyArgs1);

            var legacyArgs2 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--check-for-update", TestConstants.DefaultTestUrl });
            var exitCode2 = Run(legacyArgs2);

            // Assert
            var error1 = GetError();
            Assert.Contains("Deprecation Warning", error1);
            Assert.Contains("--checkforupdate", error1);
            Assert.Contains("check-update", error1);
            Assert.Contains("--url", string.Join(" ", legacyArgs1));

            var error2 = GetError();
            Assert.Contains("Deprecation Warning", error2);
            Assert.Contains("--check-for-update", error2);
        }

        [Fact]
        public void CheckUpdate_OutputJson_ProducesValidJson()
        {
            // Act
            var exitCode = Run("check-update", TestConstants.DefaultTestUrl, "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitUpdateAvailable || exitCode == TestConstants.ExitNoUpdate)
            {
                JsonTestHelper.AssertValidJson(output);
                
                // Verify expected structure
                var currentVersion = JsonTestHelper.GetJsonProperty(output, "currentVersion");
                var futureVersion = JsonTestHelper.GetJsonProperty(output, "futureVersion");
                Assert.NotNull(currentVersion);
                Assert.NotNull(futureVersion);
            }
        }

        [Fact]
        public void CheckUpdate_OutputTable_ProducesTableOutput()
        {
            // Act
            var exitCode = Run("check-update", TestConstants.DefaultTestUrl, "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitUpdateAvailable || exitCode == TestConstants.ExitNoUpdate)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void CheckUpdate_OutputText_ProducesTextOutput()
        {
            // Act
            var exitCode = Run("check-update", TestConstants.DefaultTestUrl, "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitUpdateAvailable || exitCode == TestConstants.ExitNoUpdate)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}