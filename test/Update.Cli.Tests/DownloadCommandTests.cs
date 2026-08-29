using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe download command parity
    /// </summary>
    public class DownloadCommandTests : CliTestBase
    {
        public DownloadCommandTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void Download_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("download", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Download releases and output JSON", output);
            Assert.Contains("<URL>", output);
            Assert.Contains("--app-name", output);
        }

        [Fact]
        public void Download_MissingUrl_ReturnsValidationError()
        {
            // Act
            var exitCode = Run("download");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Missing required argument", error);
            Assert.Contains("URL", error);
            Assert.Contains("Example:", error);
        }

        [Fact]
        public void Download_WithUrl_RunsWithoutValidationError()
        {
            // Act
            var exitCode = Run("download", TestConstants.DefaultTestUrl, "--app-name", "TestApp");

            // Assert - should not be validation error (may be system error due to network)
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Download_WithLegacySyntax_ShowsDeprecationWarningAndMapsUrl()
        {
            // Act
            var legacyArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--download", TestConstants.DefaultTestUrl });
            var exitCode = Run(legacyArgs);

            // Assert
            var error = GetError();
            Assert.Contains("Deprecation Warning", error);
            Assert.Contains("--download", error);
            Assert.Contains("download", error);
            // Verify URL was mapped to --url
            Assert.Contains("--url", string.Join(" ", legacyArgs));
        }

        [Fact]
        public void Download_OutputJson_ProducesValidJson()
        {
            // Act
            var exitCode = Run("download", TestConstants.DefaultTestUrl, "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void Download_OutputTable_ProducesTableOutput()
        {
            // Act
            var exitCode = Run("download", TestConstants.DefaultTestUrl, "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void Download_OutputText_ProducesTextOutput()
        {
            // Act
            var exitCode = Run("download", TestConstants.DefaultTestUrl, "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}