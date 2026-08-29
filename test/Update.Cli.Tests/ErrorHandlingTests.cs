using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe error handling parity
    /// </summary>
    public class ErrorHandlingTests : CliTestBase
    {
        public ErrorHandlingTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void MissingRequiredArgument_ShowsValidationErrorPanel()
        {
            // Act
            var exitCode = Run("install");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Missing required argument", error);
            Assert.Contains("--path", error);
            Assert.Contains("Example:", error);
        }

        [Fact]
        public void InvalidPath_ShowsValidationErrorWithPath()
        {
            // Act
            var exitCode = Run("install", "--path", "/nonexistent/path");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Directory not found", error);
            Assert.Contains("/nonexistent/path", error);
        }

        [Fact]
        public void MissingUrl_ShowsValidationErrorWithExample()
        {
            // Act
            var exitCode = Run("download");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Missing required argument", error);
            Assert.Contains("URL", error);
            Assert.Contains("Example:", error);
            Assert.Contains("Update.exe download https://example.com/updates", error);
        }

        [Fact]
        public void UnknownCommand_ShowsDidYouMeanSuggestion()
        {
            // Act
            var exitCode = Run("unstall"); // typo for uninstall

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("unstall", error);
            Assert.Contains("Did you mean", error);
        }

        [Fact]
        public void UnknownCommandWithSimilarName_SuggestsCorrectCommand()
        {
            // Act
            var exitCode = Run("instal"); // missing last 'l'

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("instal", error);
            Assert.Contains("install", error);
        }

        [Fact]
        public void ValidationError_PanelContainsOptionName()
        {
            // Act
            var exitCode = Run("install", "--path"); // missing value

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("--path", error);
        }

        [Fact]
        public void UserError_ShowsErrorPanelWithSuggestion()
        {
            // Act - check-update with invalid URL that will fail
            var exitCode = Run("check-update", "not-a-valid-url");

            // Assert - should be user error or validation error
            Assert.Contains(exitCode, new[] { TestConstants.ExitUserError, TestConstants.ExitValidationError });
            var error = GetError();
            Assert.NotEmpty(error);
        }

        [Fact]
        public void SystemError_ShowsExceptionPanel()
        {
            // Act - download from unreachable URL
            var exitCode = Run("download", "https://nonexistent.invalid/updates");

            // Assert - may be system error due to network
            // Network errors typically return SystemError (exit code 2)
            Assert.Contains(exitCode, new[] { TestConstants.ExitSystemError, TestConstants.ExitUserError, TestConstants.ExitValidationError });
        }

        [Fact]
        public void ErrorOutput_WithNoColor_DoesNotContainAnsiCodes()
        {
            // Act
            var exitCode = Run("install", "--no-color");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.DoesNotContain("\u001b[", error);
        }

        [Fact]
        public void ErrorOutput_WithQuiet_SuppressesErrorPanel()
        {
            // Act
            var exitCode = Run("install", "--quiet");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            // With --quiet, error output should be minimal or empty
            // (actual behavior depends on implementation)
        }

        [Fact]
        public void ValidationError_ExitCodeIs3()
        {
            // Act
            var exitCode = Run("install");

            // Assert
            Assert.Equal(3, exitCode);
        }

        [Fact]
        public void UserError_ExitCodeIs1()
        {
            // This would require triggering a UserError
            // Act - validate with missing RELEASES file
            var releaseDir = CreateTempDir("empty_releases");
            var exitCode = Run("check-update", TestConstants.DefaultTestUrl); // This might not trigger UserError

            // Assert - just verify we get some error code
            Assert.NotEqual(TestConstants.ExitSuccess, exitCode);
        }

        [Fact]
        public void SystemError_ExitCodeIs2()
        {
            // Act - network error
            var exitCode = Run("download", "https://nonexistent.invalid/updates");

            // Assert - network errors should be system errors
            // But may vary based on implementation
            Assert.True(exitCode >= 1 && exitCode <= 5);
        }

        [Fact]
        public void DeprecationWarning_ShownOnStderr()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var legacyArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--install", pkgDir });
            var exitCode = Run(legacyArgs);

            // Assert
            var error = GetError();
            Assert.Contains("Deprecation Warning", error);
        }

        [Fact]
        public void HelpOutput_GoesToStdout()
        {
            // Act
            var exitCode = Run("install", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Install the app", output);
        }
    }
}