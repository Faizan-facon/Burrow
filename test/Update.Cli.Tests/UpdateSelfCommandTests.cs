using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe update-self command parity
    /// </summary>
    public class UpdateSelfCommandTests : CliTestBase
    {
        public UpdateSelfCommandTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void UpdateSelf_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("update-self", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Self-update Update.exe", output);
            Assert.Contains("--target", output);
        }

        [Fact]
        public void UpdateSelf_NoArgs_RunsWithoutValidationError()
        {
            // Act
            var exitCode = Run("update-self");

            // Assert - should not be validation error (no required args)
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void UpdateSelf_WithTarget_ParsesCorrectly()
        {
            // Act
            var targetPath = Path.Combine(CreateTempDir("updateself"), "Update.exe");
            var exitCode = Run("update-self", "--target", targetPath);

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void UpdateSelf_WithLegacySyntax_ShowsDeprecationWarning()
        {
            // Test both legacy variants
            var deprecationOutput1 = new StringWriter();
            var legacyArgs1 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--updateself" }, deprecationOutput1);
            var exitCode1 = Run(legacyArgs1);

            var deprecationOutput2 = new StringWriter();
            var legacyArgs2 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--update-self" }, deprecationOutput2);
            var exitCode2 = Run(legacyArgs2);

            // Assert - check deprecation warning was emitted
            var error1 = deprecationOutput1.ToString();
            Assert.Contains("[DEPRECATION] Legacy syntax detected", error1);
            Assert.Contains("--updateself", error1);

            var error2 = deprecationOutput2.ToString();
            Assert.Contains("[DEPRECATION] Legacy syntax detected", error2);
            Assert.Contains("--update-self", error2);
        }

    }

    /// <summary>
    /// Tests for Update.exe process-start command parity
    /// </summary>
    public class ProcessStartCommandTests : CliTestBase
    {
        public ProcessStartCommandTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void ProcessStart_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("process-start", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Start an executable in the latest version of the app package", output);
            Assert.Contains("<EXE-NAME>", output);
            Assert.Contains("--args", output);
            Assert.Contains("--wait", output);
        }

        [Fact]
        public void ProcessStart_MissingExeName_ReturnsValidationError()
        {
            // Act
            var exitCode = Run("process-start");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
            var error = GetError();
            Assert.Contains("Missing required argument", error);
            Assert.Contains("<EXE-NAME>", error);
            Assert.Contains("Example:", error);
        }

        [Fact]
        public void ProcessStart_WithExeName_RunsWithoutValidationError()
        {
            // Act
            var exitCode = Run("process-start", "MyApp.exe");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void ProcessStart_WithArgs_ParsesCorrectly()
        {
            // Act
            var exitCode = Run("process-start", "MyApp.exe", "--args", "--flag value");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void ProcessStart_WithWait_FlagIsRecognized()
        {
            // Act
            var exitCode = Run("process-start", "MyApp.exe", "--wait");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void ProcessStart_WithLegacySyntax_ShowsDeprecationWarning()
        {
            // Test both legacy variants
            var legacyArgs1 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--processstart", "MyApp.exe" });
            var exitCode1 = Run(legacyArgs1);

            var legacyArgs2 = LegacySyntaxHelper.MapLegacyToNew(new[] { "--process-start", "MyApp.exe" });
            var exitCode2 = Run(legacyArgs2);

            // Assert
            var error1 = GetError();
            Assert.Contains("Deprecation Warning", error1);
            Assert.Contains("--processstart", error1);
            Assert.Contains("process-start", error1);

            var error2 = GetError();
            Assert.Contains("Deprecation Warning", error2);
            Assert.Contains("--process-start", error2);
        }

        [Fact]
        public void ProcessStart_OutputJson_ProducesValidJson()
        {
            // Act
            var exitCode = Run("process-start", "MyApp.exe", "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void ProcessStart_OutputTable_ProducesTableOutput()
        {
            // Act
            var exitCode = Run("process-start", "MyApp.exe", "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("┌", output);
            }
        }

        [Fact]
        public void ProcessStart_OutputText_ProducesTextOutput()
        {
            // Act
            var exitCode = Run("process-start", "MyApp.exe", "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}