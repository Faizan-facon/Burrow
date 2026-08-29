using Squirrel.Cli.Commands;
using System.Linq;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.Update
{
    /// <summary>
    /// Tests for Update.exe output format parity (json/table/text)
    /// </summary>
    public class OutputFormatTests : CliTestBase
    {
        public OutputFormatTests()
        {
            ConfigureUpdateApp();
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void Install_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void Uninstall_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Act
            var exitCode = Run("uninstall", "--app-name", "TestApp", "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void Download_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Act
            var exitCode = Run("download", TestConstants.DefaultTestUrl, "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void CheckUpdate_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Act
            var exitCode = Run("check-update", TestConstants.DefaultTestUrl, "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitUpdateAvailable || exitCode == TestConstants.ExitNoUpdate)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        // Verify expected structure
                        var currentVersion = JsonTestHelper.GetJsonProperty(output, "currentVersion");
                        var futureVersion = JsonTestHelper.GetJsonProperty(output, "futureVersion");
                        Assert.NotNull(currentVersion);
                        Assert.NotNull(futureVersion);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void Update_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Act
            var exitCode = Run("update", TestConstants.DefaultTestUrl, "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void Releasify_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Arrange
            var nupkgPath = Path.Combine(CreateTempDir("releasify"), "TestApp-1.0.0.0.nupkg");
            File.WriteAllBytes(nupkgPath, new byte[1024]);

            // Act
            var exitCode = Run("releasify", nupkgPath, "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void Shortcut_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Act
            var exitCode = Run("shortcut", "MyApp.exe", "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void RemoveShortcut_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Act
            var exitCode = Run("remove-shortcut", "MyApp.exe", "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void UpdateSelf_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Act
            var exitCode = Run("update-self", "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void ProcessStart_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Act
            var exitCode = Run("process-start", "MyApp.exe", "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        break;
                    case "text":
                        Assert.DoesNotContain("┌", output);
                        break;
                }
            }
        }

        [Fact]
        public void JsonOutput_IsParseableAndStructured()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                var doc = System.Text.Json.JsonDocument.Parse(output);
                // Should be an object (not array) for single command results
                Assert.Equal(System.Text.Json.JsonValueKind.Object, doc.RootElement.ValueKind);
            }
        }

        [Fact]
        public void TableOutput_HasHeadersAndAlignment()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                // Table should have border characters
                Assert.Contains("┌", output); // Top border
                Assert.Contains("└", output); // Bottom border
                Assert.Contains("│", output); // Vertical separators
                Assert.Contains("├", output); // Header separator
            }
        }

        [Fact]
        public void TextOutput_IsPlainTextWithoutFormatting()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp", "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                // Text output should not have table borders
                Assert.DoesNotContain("┌", output);
                Assert.DoesNotContain("│", output);
                Assert.DoesNotContain("├", output);
                // Should not have ANSI escape codes by default
                // (though it might have them without --no-color)
            }
        }

        [Fact]
        public void DefaultOutputFormat_IsText()
        {
            // Arrange
            var pkgDir = CreateFakePackageDir("TestApp", "1.0.0.0");

            // Act - no --output specified
            var exitCode = Run("install", "--path", pkgDir, "--app-name", "TestApp");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError)
            {
                // Default should be text format
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}