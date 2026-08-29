using Squirrel.Cli.Commands;
using System.Linq;
using System;
using System.IO;
using Squirrel.Cli.Tests;
using Xunit;

namespace Squirrel.Cli.Tests.SyncReleases
{
    /// <summary>
    /// Tests for SyncReleases.exe output format parity (json/table/text)
    /// </summary>
    public class OutputFormatTests : SyncReleasesCliTestBase
    {
        [Theory]
        [InlineData("json")]
        [InlineData("table")]
        [InlineData("text")]
        public void Sync_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Arrange
            var releaseDir = CreateTempDir("releases");

            // Act
            var exitCode = Run("sync", "--url", TestConstants.DefaultTestUrl, "--release-dir", releaseDir, "--output", format);

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
        public void Validate_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("validate", "--release-dir", releaseDir, "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
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
        public void List_AllOutputFormats_ProduceExpectedOutput(string format)
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(3, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--output", format);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                switch (format)
                {
                    case "json":
                        JsonTestHelper.AssertValidJson(output);
                        // Verify array structure
                        var doc = System.Text.Json.JsonDocument.Parse(output);
                        Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
                        break;
                    case "table":
                        Assert.Contains("┌", output);
                        Assert.Contains("version", output);
                        Assert.Contains("filename", output);
                        Assert.Contains("size", output);
                        Assert.Contains("type", output);
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
            var releaseDir = CreateFakeReleasesDir(2, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--output", "json");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                var doc = System.Text.Json.JsonDocument.Parse(output);
                Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
                
                if (doc.RootElement.GetArrayLength() > 0)
                {
                    var first = doc.RootElement[0];
                    Assert.True(first.TryGetProperty("version", out _));
                    Assert.True(first.TryGetProperty("filename", out _));
                    Assert.True(first.TryGetProperty("size", out _));
                    Assert.True(first.TryGetProperty("type", out _));
                }
            }
        }

        [Fact]
        public void TableOutput_HasHeadersAndAlignment()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(2, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--output", "table");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
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
            var releaseDir = CreateFakeReleasesDir(2, "TestApp");

            // Act
            var exitCode = Run("list", "--release-dir", releaseDir, "--output", "text");

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                // Text output should not have table borders
                Assert.DoesNotContain("┌", output);
                Assert.DoesNotContain("│", output);
                Assert.DoesNotContain("├", output);
            }
        }

        [Fact]
        public void DefaultOutputFormat_IsText()
        {
            // Arrange
            var releaseDir = CreateFakeReleasesDir(1, "TestApp");

            // Act - no --output specified
            var exitCode = Run("list", "--release-dir", releaseDir);

            // Assert
            var output = GetOutput();
            if (exitCode == TestConstants.ExitSuccess || exitCode == TestConstants.ExitSystemError || exitCode == TestConstants.ExitUserError)
            {
                // Default should be text format
                Assert.DoesNotContain("┌", output);
            }
        }
    }
}