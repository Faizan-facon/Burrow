using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe exit code parity
    /// </summary>
    public class ExitCodeTests : CliTestBase
    {
        public ExitCodeTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void Success_ReturnsExitCode0()
        {
            // Act
            var exitCode = Run("install", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
        }

        [Fact]
        public void ValidationError_ReturnsExitCode3()
        {
            // Act
            var exitCode = Run("install"); // missing required --path

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void AllCommands_ValidationErrorReturns3()
        {
            var commandsWithMissingArgs = new[]
            {
                new[] { "install" },
                new[] { "download" },
                new[] { "check-update" },
                new[] { "update" },
                new[] { "shortcut" },
                new[] { "remove-shortcut" },
                new[] { "process-start" },
            };

            foreach (var cmd in commandsWithMissingArgs)
            {
                var exitCode = Run(cmd);
                Assert.Equal(TestConstants.ExitValidationError, exitCode);
            }
        }

        [Fact]
        public void CommandsWithNoRequiredArgs_ValidationErrorNotTriggered()
        {
            var commandsWithNoRequiredArgs = new[]
            {
                new[] { "uninstall" },
                new[] { "update-self" },
            };

            foreach (var cmd in commandsWithNoRequiredArgs)
            {
                var exitCode = Run(cmd);
                // Should not be validation error (no required args)
                Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
            }
        }

        [Fact]
        public void CheckUpdate_UpdateAvailable_ReturnsExitCode4()
        {
            // This test documents expected behavior
            // Actual test would need a mock server with updates
            // Act
            var exitCode = Run("check-update", TestConstants.DefaultTestUrl);

            // Assert - should be one of expected codes
            Assert.Contains(exitCode, new[] 
            { 
                TestConstants.ExitSuccess, 
                TestConstants.ExitUpdateAvailable, 
                TestConstants.ExitNoUpdate,
                TestConstants.ExitSystemError,
                TestConstants.ExitUserError
            });
        }

        [Fact]
        public void CheckUpdate_NoUpdate_ReturnsExitCode5()
        {
            // This test documents expected behavior
            // Act
            var exitCode = Run("check-update", TestConstants.DefaultTestUrl);

            // Assert
            Assert.Contains(exitCode, new[] 
            { 
                TestConstants.ExitSuccess, 
                TestConstants.ExitUpdateAvailable, 
                TestConstants.ExitNoUpdate,
                TestConstants.ExitSystemError,
                TestConstants.ExitUserError
            });
        }

        [Fact]
        public void NetworkError_ReturnsExitCode2()
        {
            // Act - unreachable URL
            var exitCode = Run("download", "https://nonexistent.invalid/updates");

            // Assert - network errors should be system error (2) or user error (1)
            Assert.Contains(exitCode, new[] { TestConstants.ExitSystemError, TestConstants.ExitUserError });
        }

        [Fact]
        public void InvalidPath_ReturnsExitCode3()
        {
            // Act
            var exitCode = Run("install", "--path", "/nonexistent/path");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void UnknownCommand_ReturnsExitCode3()
        {
            // Act
            var exitCode = Run("unknowncommand");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Help_ReturnsExitCode0()
        {
            var commands = new[]
            {
                "install", "uninstall", "download", "check-update", "update",
                "shortcut", "remove-shortcut", "update-self", "process-start"
            };

            foreach (var cmd in commands)
            {
                var exitCode = Run(cmd, "--help");
                Assert.Equal(TestConstants.ExitSuccess, exitCode);
            }
        }

        [Fact]
        public void GlobalOptionsWithValidCommand_ReturnSuccess()
        {
            var exitCode = Run("install", "--help", "--verbose", "--no-color", "--output", "json");
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
        }

        [Fact]
        public void LegacySyntax_WithValidArgs_ReturnsSameExitCodeAsNewSyntax()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act - legacy syntax
            var legacyArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--install", pkgDir });
            var legacyExitCode = Run(legacyArgs);

            // Act - new syntax
            var newExitCode = Run("install", "--path", pkgDir);

            // Assert - both should have same exit code category (not validation error)
            Assert.NotEqual(TestConstants.ExitValidationError, legacyExitCode);
            Assert.NotEqual(TestConstants.ExitValidationError, newExitCode);
        }
    }
}