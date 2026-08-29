using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe update command parity
    /// </summary>
    public class UpdateCommandTests : CliTestBase
    {
        public UpdateCommandTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void Update_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("update", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Update to latest version", output);
            Assert.Contains("<URL>", output);
            Assert.Contains("--app-name", output);
            Assert.Contains("--ignore-delta", output);
        }

        [Fact]
        public void Update_MissingUrl_ReturnsValidationError()
        {
            // Act
            var exitCode = Run("update");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Missing required argument", error);
            Assert.Contains("URL", error);
            Assert.Contains("Example:", error);
        }

        [Fact]
        public void Update_WithUrl_RunsWithoutValidationError()
        {
            // Act
            var exitCode = Run("update", TestConstants.DefaultTestUrl, "--app-name", "TestApp");

            // Assert - should not be validation error (may be system error due to network)
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Update_WithIgnoreDelta_FlagIsRecognized()
        {
            // Act
            var exitCode = Run("update", TestConstants.DefaultTestUrl, "--ignore-delta");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Update_WithLegacySyntax_ShowsDeprecationWarningAndMapsUrl()
        {
            // Act
            var legacyArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--update", TestConstants.DefaultTestUrl });
            var exitCode = Run(legacyArgs);

            // Assert
            var error = GetError();
            Assert.Contains("Deprecation Warning", error);
            Assert.Contains("--update", error);
            Assert.Contains("update", error);
            Assert.Contains("--url", string.Join(" ", legacyArgs));
        }

        [Fact]
        public void Update_OutputJson_ProducesValidJson()
        {
            // Act
            var exitCode = Run("update", TestConstants.DefaultTestUrl, "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void Update_OutputTable_ProducesTableOutput()
        {
            // Act
            var exitCode = Run("update", TestConstants.DefaultTestUrl, "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void Update_OutputText_ProducesTextOutput()
        {
            // Act
            var exitCode = Run("update", TestConstants.DefaultTestUrl, "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}