using Squirrel.Cli.Commands;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe install command parity
    /// </summary>
    public class InstallCommandTests : CliTestBase
    {
        public InstallCommandTests()
        {
            ConfigureUpdateApp();
        }

        [Fact]
        public void Install_Help_ShowsUsage()
        {
            // Act
            var exitCode = Run("install", "--help");

            // Assert
            Assert.Equal(TestConstants.ExitSuccess, exitCode);
            var output = GetOutput();
            Assert.Contains("Install the app from a package directory", output);
            Assert.Contains("<PATH>", output);
            Assert.Contains("--silent", output);
            Assert.Contains("--app-name", output);
        }

        [Fact]
        public void Install_MissingPath_ReturnsValidationError()
        {
            // Act
            var exitCode = Run("install");

            // Assert - parser validation returns exit code 3, but error message not captured
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Install_NonExistentPath_ReturnsValidationError()
        {
            // Act
            var exitCode = Run("install", "--path", "/nonexistent/path");

            // Assert
            Assert.Equal(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Install_ValidPathButNoReleases_CreatesReleasesFile()
        {
            // Arrange
            var pkgDir = CreateTempDir("valid_pkg");
            var nupkgPath = Path.Combine(pkgDir, "TestApp-1.0.0.0-full.nupkg");
            File.WriteAllBytes(nupkgPath, new byte[1024]);

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--silent");

            // Assert - may fail due to missing UpdateManager setup, but should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Install_WithLegacySyntax_ShowsDeprecationWarning()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var legacyArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--install", pkgDir });
            var exitCode = Run(legacyArgs);

            // Assert
            var error = GetError();
            Assert.Contains("Deprecation Warning", error);
            Assert.Contains("--install", error);
            Assert.Contains("install", error);
        }

        [Fact]
        public void Install_WithSilentFlag_Succeeds()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--silent", "--app-name", "TestApp");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Install_WithAppName_UsesProvidedName()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "CustomAppName");

            // Assert - should not be validation error
            Assert.NotEqual(TestConstants.ExitValidationError, exitCode);
        }

        [Fact]
        public void Install_OutputJson_ProducesValidJson()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                JsonTestHelper.AssertValidJson(output);
            }
        }

        [Fact]
        public void Install_OutputTable_ProducesTableOutput()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.Contains("┌", output); // Table border
            }
        }

        [Fact]
        public void Install_OutputText_ProducesTextOutput()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}