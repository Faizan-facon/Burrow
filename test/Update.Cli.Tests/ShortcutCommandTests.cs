using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe shortcut command parity
    /// </summary>
    public class ShortcutCommandTests : CliTestBase
    {
        public ShortcutCommandTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void Shortcut_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("shortcut", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Create a shortcut for the given executable", output);
            Assert.Contains("<EXE-NAME>", output);
            Assert.Contains("--shortcut-locations", output);
            Assert.Contains("--process-start-args", output);
            Assert.Contains("--icon", output);
            Assert.Contains("--update-only", output);
        }

        [Fact]
        public void Shortcut_MissingExeName_ReturnsValidationError()
        {
            // Act
            var exitCode = Run("shortcut");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Missing required argument", error);
            Assert.Contains("<EXE-NAME>", error);
            Assert.Contains("Example:", error);
        }

        [Fact]
        public void Shortcut_WithExeName_RunsWithoutValidationError()
        {
            // Act
            var exitCode = Run("shortcut", "MyApp.exe");

            // Assert - should not be validation error (may fail for other reasons)
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Shortcut_WithShortcutLocations_ParsesCorrectly()
        {
            // Act
            var exitCode = Run("shortcut", "MyApp.exe", "--shortcut-locations", "Desktop,StartMenu");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Shortcut_WithProcessStartArgs_ParsesCorrectly()
        {
            // Act
            var exitCode = Run("shortcut", "MyApp.exe", "--process-start-args", "--flag value");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Shortcut_WithIcon_ParsesCorrectly()
        {
            // Act
            var exitCode = Run("shortcut", "MyApp.exe", "--icon", "icon.ico");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Shortcut_WithUpdateOnly_FlagIsRecognized()
        {
            // Act
            var exitCode = Run("shortcut", "MyApp.exe", "--update-only");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Shortcut_WithLegacySyntax_ShowsDeprecationWarning()
        {
            // Test both legacy variants
            var legacyArgs1 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--createshortcut", "MyApp.exe" });
            var exitCode1 = Run(legacyArgs1);

            var legacyArgs2 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--create-shortcut", "MyApp.exe" });
            var exitCode2 = Run(legacyArgs2);

            // Assert
            var error1 = GetError();
            Assert.Contains("Deprecation Warning", error1);
            Assert.Contains("--createshortcut", error1);
            Assert.Contains("shortcut", error1);

            var error2 = GetError();
            Assert.Contains("Deprecation Warning", error2);
            Assert.Contains("--create-shortcut", error2);
        }

        [Fact]
        public void Shortcut_OutputJson_ProducesValidJson()
        {
            // Act
            var exitCode = Run("shortcut", "MyApp.exe", "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void Shortcut_OutputTable_ProducesTableOutput()
        {
            // Act
            var exitCode = Run("shortcut", "MyApp.exe", "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void Shortcut_OutputText_ProducesTextOutput()
        {
            // Act
            var exitCode = Run("shortcut", "MyApp.exe", "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }

    /// <summary>
    /// Tests for Update.exe remove-shortcut command parity
    /// </summary>
    public class RemoveShortcutCommandTests : CliTestBase
    {
        public RemoveShortcutCommandTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void RemoveShortcut_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("remove-shortcut", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Remove a shortcut for the given executable", output);
            Assert.Contains("<EXE-NAME>", output);
            Assert.Contains("--shortcut-locations", output);
        }

        [Fact]
        public void RemoveShortcut_MissingExeName_ReturnsValidationError()
        {
            // Act
            var exitCode = Run("remove-shortcut");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Missing required argument", error);
            Assert.Contains("<EXE-NAME>", error);
            Assert.Contains("Example:", error);
        }

        [Fact]
        public void RemoveShortcut_WithExeName_RunsWithoutValidationError()
        {
            // Act
            var exitCode = Run("remove-shortcut", "MyApp.exe");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void RemoveShortcut_WithShortcutLocations_ParsesCorrectly()
        {
            // Act
            var exitCode = Run("remove-shortcut", "MyApp.exe", "--shortcut-locations", "Desktop,StartMenu");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void RemoveShortcut_WithLegacySyntax_ShowsDeprecationWarning()
        {
            // Test both legacy variants
            var legacyArgs1 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--removeshortcut", "MyApp.exe" });
            var exitCode1 = Run(legacyArgs1);

            var legacyArgs2 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--remove-shortcut", "MyApp.exe" });
            var exitCode2 = Run(legacyArgs2);

            // Assert
            var error1 = GetError();
            Assert.Contains("Deprecation Warning", error1);
            Assert.Contains("--removeshortcut", error1);
            Assert.Contains("remove-shortcut", error1);

            var error2 = GetError();
            Assert.Contains("Deprecation Warning", error2);
            Assert.Contains("--remove-shortcut", error2);
        }

        [Fact]
        public void RemoveShortcut_OutputJson_ProducesValidJson()
        {
            // Act
            var exitCode = Run("remove-shortcut", "MyApp.exe", "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void RemoveShortcut_OutputTable_ProducesTableOutput()
        {
            // Act
            var exitCode = Run("remove-shortcut", "MyApp.exe", "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void RemoveShortcut_OutputText_ProducesTextOutput()
        {
            // Act
            var exitCode = Run("remove-shortcut", "MyApp.exe", "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}