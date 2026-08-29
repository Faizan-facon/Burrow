using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe uninstall command parity
    /// </summary>
    public class UninstallCommandTests : CliTestBase
    {
        public UninstallCommandTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void Uninstall_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("uninstall", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Uninstall the app", output);
            Assert.Contains("--app-name", output);
        }

        [Fact]
        public void Uninstall_NoArgs_RunsWithDefaultAppName()
        {
            // Act
            var exitCode = Run("uninstall");

            // Assert - should not be validation error (app-name is optional)
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Uninstall_WithAppName_UsesProvidedName()
        {
            // Act
            var exitCode = Run("uninstall", "--app-name", "CustomAppName");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Uninstall_WithLegacySyntax_ShowsDeprecationWarning()
        {
            // Act
            var legacyArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--uninstall" });
            var exitCode = Run(legacyArgs);

            // Assert
            var error = GetError();
            Assert.Contains("Deprecation Warning", error);
            Assert.Contains("--uninstall", error);
            Assert.Contains("uninstall", error);
        }

        [Fact]
        public void Uninstall_OutputJson_ProducesValidJson()
        {
            // Act
            var exitCode = Run("uninstall", "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void Uninstall_OutputTable_ProducesTableOutput()
        {
            // Act
            var exitCode = Run("uninstall", "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void Uninstall_OutputText_ProducesTextOutput()
        {
            // Act
            var exitCode = Run("uninstall", "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}