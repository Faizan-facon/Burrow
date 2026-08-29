using Squirrel.Cli.Tests;
using System.Linq;
using System.IO;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for legacy syntax mapping parity
    /// </summary>
    public class LegacySyntaxMappingTests : CliTestBase
    {
        public LegacySyntaxMappingTests()
        {
            ConfigureUpdateApp();
        }

        [Theory]
        [InlineData("--install", "install")]
        [InlineData("--uninstall", "uninstall")]
        [InlineData("--download", "download")]
        [InlineData("--checkforupdate", "check-update")]
        [InlineData("--check-for-update", "check-update")]
        [InlineData("--update", "update")]
        [InlineData("--releasify", "releasify")]
        [InlineData("--createshortcut", "shortcut")]
        [InlineData("--create-shortcut", "shortcut")]
        [InlineData("--removeshortcut", "remove-shortcut")]
        [InlineData("--remove-shortcut", "remove-shortcut")]
        [InlineData("--updateself", "update-self")]
        [InlineData("--update-self", "update-self")]
        [InlineData("--processstart", "process-start")]
        [InlineData("--process-start", "process-start")]
        public void LegacySyntax_MapsToCorrectCommand(string legacyFlag, string expectedCommand)
        {
            // Arrange
            var args = new[] { legacyFlag };
            if (legacyFlag != "--uninstall" && legacyFlag != "--updateself" && legacyFlag != "--update-self")
            {
                args = new[] { legacyFlag, "testvalue" };
            }

            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(args);

            // Assert
            Assert.Equal(expectedCommand, mappedArgs[0]);
        }

        [Fact]
        public void LegacySyntax_Install_MapsPathArgument()
        {
            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--install", "./packages" });

            // Assert
            Assert.Equal("install", mappedArgs[0]);
            Assert.Equal("./packages", mappedArgs[1]);
        }

        [Fact]
        public void LegacySyntax_Download_MapsUrlToUrlOption()
        {
            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--download", "https://example.com/updates" });

            // Assert
            Assert.Equal("download", mappedArgs[0]);
            Assert.Equal("--url", mappedArgs[1]);
            Assert.Equal("https://example.com/updates", mappedArgs[2]);
        }

        [Fact]
        public void LegacySyntax_CheckForUpdate_MapsUrlToUrlOption()
        {
            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--checkforupdate", "https://example.com/updates" });

            // Assert
            Assert.Equal("check-update", mappedArgs[0]);
            Assert.Equal("--url", mappedArgs[1]);
            Assert.Equal("https://example.com/updates", mappedArgs[2]);
        }

        [Fact]
        public void LegacySyntax_Update_MapsUrlToUrlOption()
        {
            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--update", "https://example.com/updates" });

            // Assert
            Assert.Equal("update", mappedArgs[0]);
            Assert.Equal("--url", mappedArgs[1]);
            Assert.Equal("https://example.com/updates", mappedArgs[2]);
        }

        [Fact]
        public void LegacySyntax_Releasify_MapsPackageArgument()
        {
            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--releasify", "package.nupkg" });

            // Assert
            Assert.Equal("releasify", mappedArgs[0]);
            Assert.Equal("package.nupkg", mappedArgs[1]);
        }

        [Fact]
        public void LegacySyntax_Shortcut_MapsExeNameArgument()
        {
            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--createshortcut", "MyApp.exe" });

            // Assert
            Assert.Equal("shortcut", mappedArgs[0]);
            Assert.Equal("MyApp.exe", mappedArgs[1]);
        }

        [Fact]
        public void LegacySyntax_RemoveShortcut_MapsExeNameArgument()
        {
            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--removeshortcut", "MyApp.exe" });

            // Assert
            Assert.Equal("remove-shortcut", mappedArgs[0]);
            Assert.Equal("MyApp.exe", mappedArgs[1]);
        }

        [Fact]
        public void LegacySyntax_ProcessStart_MapsExeNameArgument()
        {
            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--processstart", "MyApp.exe" });

            // Assert
            Assert.Equal("process-start", mappedArgs[0]);
            Assert.Equal("MyApp.exe", mappedArgs[1]);
        }

        [Fact]
        public void LegacySyntax_ProducesDeprecationWarning()
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
        public void NonLegacyArgs_PassedThroughUnchanged()
        {
            // Act
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "install", "./packages", "--silent" });

            // Assert
            Assert.Equal(new[] { "install", "./packages", "--silent" }, mappedArgs);
        }

        [Fact]
        public void MixedLegacyAndNewArgs_LegacyMappedNewPreserved()
        {
            // Act - legacy install with new --silent flag
            var mappedArgs = LegacySyntaxHelper.MapLegacyToNew(new[] { "--install", "./packages", "--silent" });

            // Assert
            Assert.Equal("install", mappedArgs[0]);
            Assert.Equal("./packages", mappedArgs[1]);
            Assert.Equal("--silent", mappedArgs[2]);
        }
    }
}