using System;
using System.IO;
using System.Linq;
using Squirrel;
using Squirrel.Tests.TestHelpers;
using Xunit;
using NuGet;

using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Squirrel.Json;

namespace Squirrel.Tests.Core
{
    public class ReleaseEntryTests
    {
        [Theory]
        [InlineData(@"94689fede03fed7ab59c24337673a27837f0c3ec MyCoolApp-1.0.nupkg 1004502", "MyCoolApp-1.0.nupkg", 1004502, null, null)]
        [InlineData(@"3a2eadd15dd984e4559f2b4d790ec8badaeb6a39   MyCoolApp-1.1.nupkg   1040561", "MyCoolApp-1.1.nupkg", 1040561, null, null)]
        [InlineData(@"14db31d2647c6d2284882a2e101924a9c409ee67  MyCoolApp-1.1.nupkg.delta  80396", "MyCoolApp-1.1.nupkg.delta", 80396, null, null)]
        [InlineData(@"0000000000000000000000000000000000000000  http://test.org/Folder/MyCoolApp-1.2.nupkg  2569", "MyCoolApp-1.2.nupkg", 2569, "http://test.org/Folder/", null)]
        [InlineData(@"0000000000000000000000000000000000000000  http://test.org/Folder/MyCoolApp-1.2.nupkg?query=param  2569", "MyCoolApp-1.2.nupkg", 2569, "http://test.org/Folder/", "?query=param")]
        [InlineData(@"0000000000000000000000000000000000000000  https://www.test.org/Folder/MyCoolApp-1.2-delta.nupkg  1231953", "MyCoolApp-1.2-delta.nupkg", 1231953, "https://www.test.org/Folder/", null)]
        [InlineData(@"0000000000000000000000000000000000000000  https://www.test.org/Folder/MyCoolApp-1.2-delta.nupkg?query=param  1231953", "MyCoolApp-1.2-delta.nupkg", 1231953, "https://www.test.org/Folder/", "?query=param")]
        public void ParseValidReleaseEntryLines(string releaseEntry, string fileName, long fileSize, string baseUrl, string query)
        {
            var fixture = ReleaseEntry.ParseReleaseEntry(releaseEntry);
            Assert.Equal(fileName, fixture.Filename);
            Assert.Equal(fileSize, fixture.Filesize);
            Assert.Equal(baseUrl, fixture.BaseUrl);
            Assert.Equal(query, fixture.Query);
        }

        [Theory]
        [InlineData(@"0000000000000000000000000000000000000000  file:/C/Folder/MyCoolApp-0.0.nupkg  0")]
        [InlineData(@"0000000000000000000000000000000000000000  C:\Folder\MyCoolApp-0.0.nupkg  0")]
        [InlineData(@"0000000000000000000000000000000000000000  ..\OtherFolder\MyCoolApp-0.0.nupkg  0")]
        [InlineData(@"0000000000000000000000000000000000000000  ../OtherFolder/MyCoolApp-0.0.nupkg  0")]
        [InlineData(@"0000000000000000000000000000000000000000  \\Somewhere\NetworkShare\MyCoolApp-0.0.nupkg.delta  0")]
        public void ParseThrowsWhenInvalidReleaseEntryLines(string releaseEntry)
        {
            Assert.Throws<Exception>(() => ReleaseEntry.ParseReleaseEntry(releaseEntry));
        }

        [Theory]
        [InlineData(@"0000000000000000000000000000000000000000 file.nupkg 0")]
        [InlineData(@"0000000000000000000000000000000000000000 http://path/file.nupkg 0")]
        public void EntryAsStringMatchesParsedInput(string releaseEntry)
        {
            var fixture = ReleaseEntry.ParseReleaseEntry(releaseEntry);
            Assert.Equal(releaseEntry, fixture.EntryAsString);
        }

        [Theory]
        [InlineData("Squirrel.Core.1.0.0.0.nupkg", 4457, "75255cfd229a1ed1447abe1104f5635e69975d30")]
        [InlineData("Squirrel.Core.1.1.0.0.nupkg", 15830, "9baf1dbacb09940086c8c62d9a9dbe69fe1f7593")]
        public void GenerateFromFileTest(string name, long size, string sha1)
        {
            var path = IntegrationTestHelper.GetPath("fixtures", name);

            using (var f = File.OpenRead(path)) {
                var fixture = ReleaseEntry.GenerateFromFile(f, "dontcare");
                Assert.Equal(size, fixture.Filesize);
                Assert.Equal(sha1, fixture.SHA1.ToLowerInvariant());
            }
        }

        [Theory]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.nupkg                  123", 1, 2, 0, 0, "", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-full.nupkg             123", 1, 2, 0, 0, "", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-delta.nupkg            123", 1, 2, 0, 0, "", true)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-beta1.nupkg            123", 1, 2, 0, 0, "beta1", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-beta1-full.nupkg       123", 1, 2, 0, 0, "beta1", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-beta1-delta.nupkg      123", 1, 2, 0, 0, "beta1", true)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.nupkg                123", 1, 2, 3, 0, "", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3-full.nupkg           123", 1, 2, 3, 0, "", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3-delta.nupkg          123", 1, 2, 3, 0, "", true)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3-beta1.nupkg          123", 1, 2, 3, 0, "beta1", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3-beta1-full.nupkg     123", 1, 2, 3, 0, "beta1", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3-beta1-delta.nupkg    123", 1, 2, 3, 0, "beta1", true)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.4.nupkg              123", 1, 2, 3, 4, "", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.4-full.nupkg         123", 1, 2, 3, 4, "", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.4-delta.nupkg        123", 1, 2, 3, 4, "", true)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.4-beta1.nupkg        123", 1, 2, 3, 4, "beta1", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.4-beta1-full.nupkg   123", 1, 2, 3, 4, "beta1", false)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.4-beta1-delta.nupkg  123", 1, 2, 3, 4, "beta1", true)]
        public void ParseVersionTest(string releaseEntry, int major, int minor, int patch, int revision, string prerelease, bool isDelta)
        {
            var fixture = ReleaseEntry.ParseReleaseEntry(releaseEntry);

            Assert.Equal(new SemanticVersion(new Version(major, minor, patch, revision), prerelease), fixture.Version);
            Assert.Equal(isDelta, fixture.IsDelta);
        }

        [Theory]
        [InlineData("0000000000000000000000000000000000000000  MyCool-App-1.2.nupkg                  123", "MyCool-App")]
        [InlineData("0000000000000000000000000000000000000000  MyCool_App-1.2-full.nupkg             123", "MyCool_App")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-delta.nupkg            123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-beta1.nupkg            123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-beta1-full.nupkg       123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-beta1-delta.nupkg      123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCool-App-1.2.3.nupkg                123", "MyCool-App")]
        [InlineData("0000000000000000000000000000000000000000  MyCool_App-1.2.3-full.nupkg           123", "MyCool_App")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3-delta.nupkg          123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3-beta1.nupkg          123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3-beta1-full.nupkg     123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3-beta1-delta.nupkg    123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCool-App-1.2.3.4.nupkg              123", "MyCool-App")]
        [InlineData("0000000000000000000000000000000000000000  MyCool_App-1.2.3.4-full.nupkg         123", "MyCool_App")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.4-delta.nupkg        123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.4-beta1.nupkg        123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.3.4-beta1-full.nupkg   123", "MyCoolApp")]
        [InlineData("0000000000000000000000000000000000000000  MyCool-App-1.2.3.4-beta1-delta.nupkg  123", "MyCool-App")]
        public void CheckPackageName(string releaseEntry, string expected)
        {
            var fixture = ReleaseEntry.ParseReleaseEntry(releaseEntry);
            Assert.Equal(expected, fixture.PackageName);
        }

        [Theory]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2.nupkg                  123 # 10%", 1, 2, 0, 0, "", false, 0.1f)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-full.nupkg             123 # 90%", 1, 2, 0, 0, "", false, 0.9f)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-delta.nupkg            123", 1, 2, 0, 0, "", true, null)]
        [InlineData("0000000000000000000000000000000000000000  MyCoolApp-1.2-delta.nupkg            123 # 5%", 1, 2, 0, 0, "", true, 0.05f)]
        public void ParseStagingPercentageTest(string releaseEntry, int major, int minor, int patch, int revision, string prerelease, bool isDelta, float? stagingPercentage)
        {
            var fixture = ReleaseEntry.ParseReleaseEntry(releaseEntry);

            Assert.Equal(new SemanticVersion(new Version(major, minor, patch, revision), prerelease), fixture.Version);
            Assert.Equal(isDelta, fixture.IsDelta);

            if (stagingPercentage.HasValue) {
                Assert.True(Math.Abs(fixture.StagingPercentage.Value - stagingPercentage.Value) < 0.001);
            } else {
                Assert.Null(fixture.StagingPercentage);
            }
        }


        [Fact]
        public void CanParseGeneratedReleaseEntryAsString()
        {
            var path = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.1.0.0.nupkg");
            var entryAsString = ReleaseEntry.GenerateFromFile(path).EntryAsString;
            ReleaseEntry.ParseReleaseEntry(entryAsString);
        }

        [Fact]
        public void InvalidReleaseNotesThrowsException()
        {
            var path = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.0.0.0.nupkg");
            var fixture = ReleaseEntry.GenerateFromFile(path);
            Assert.Throws<Exception>(() => fixture.GetReleaseNotes(IntegrationTestHelper.GetPath("fixtures")));
        }

        [Fact]
        public void GetLatestReleaseWithNullCollectionReturnsNull()
        {
            Assert.Null(ReleaseEntry.GetPreviousRelease(
                null, null, null));
        }

        [Fact]
        public void GetLatestReleaseWithEmptyCollectionReturnsNull()
        {
            Assert.Null(ReleaseEntry.GetPreviousRelease(
                Enumerable.Empty<ReleaseEntry>(), null, null));
        }

        [Fact]
        public void WhenCurrentReleaseMatchesLastReleaseReturnNull()
        {
            var package = new ReleasePackage("Espera-1.7.6-beta.nupkg");

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.7.6-beta.nupkg"))
            };
            Assert.Null(ReleaseEntry.GetPreviousRelease(
                releaseEntries, package, @"C:\temp\somefolder"));
        }

        [Fact]
        public void WhenMultipleReleaseMatchesReturnEarlierResult()
        {
            var expected = new SemanticVersion("1.7.5-beta");
            var package = new ReleasePackage("Espera-1.7.6-beta.nupkg");

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.7.6-beta.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.7.5-beta.nupkg"))
            };

            var actual = ReleaseEntry.GetPreviousRelease(
                releaseEntries,
                package,
                @"C:\temp\");

            Assert.Equal(expected, actual.Version);
        }

        [Fact]
        public void WhenMultipleReleasesFoundReturnPreviousVersion()
        {
            var expected = new SemanticVersion("1.7.6-beta");
            var input = new ReleasePackage("Espera-1.7.7-beta.nupkg");

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.7.6-beta.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.7.5-beta.nupkg"))
            };

            var actual = ReleaseEntry.GetPreviousRelease(
                releaseEntries,
                input,
                @"C:\temp\");

            Assert.Equal(expected, actual.Version);
        }

        [Fact]
        public void WhenMultipleReleasesFoundInOtherOrderReturnPreviousVersion()
        {
            var expected = new SemanticVersion("1.7.6-beta");
            var input = new ReleasePackage("Espera-1.7.7-beta.nupkg");

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.7.5-beta.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.7.6-beta.nupkg"))
            };

            var actual = ReleaseEntry.GetPreviousRelease(
                releaseEntries,
                input,
                @"C:\temp\");

            Assert.Equal(expected, actual.Version);
        }

        [Fact]
        public void WhenReleasesAreOutOfOrderSortByVersion()
        {
            var path = Path.GetTempFileName();
            var firstVersion = new SemanticVersion("1.0.0");
            var secondVersion = new SemanticVersion("1.1.0");
            var thirdVersion = new SemanticVersion("1.2.0");

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-delta.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.1.0-delta.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.1.0-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.0.0-full.nupkg"))
            };

            ReleaseEntry.WriteReleaseFile(releaseEntries, path);

            var releases = ReleaseEntry.ParseReleaseFile(File.ReadAllText(path)).ToArray();

            Assert.Equal(firstVersion, releases[0].Version);
            Assert.Equal(secondVersion, releases[1].Version);
            Assert.Equal(true, releases[1].IsDelta);
            Assert.Equal(secondVersion, releases[2].Version);
            Assert.Equal(false, releases[2].IsDelta);
            Assert.Equal(thirdVersion, releases[3].Version);
            Assert.Equal(true, releases[3].IsDelta);
            Assert.Equal(thirdVersion, releases[4].Version);
            Assert.Equal(false, releases[4].IsDelta);
        }

        [Fact]
        public void WhenPreReleasesAreOutOfOrderSortByNumericSuffix()
        {
            var path = Path.GetTempFileName();
            var firstVersion = new SemanticVersion("1.1.9-beta105");
            var secondVersion = new SemanticVersion("1.2.0-beta9");
            var thirdVersion = new SemanticVersion("1.2.0-beta10");
            var fourthVersion = new SemanticVersion("1.2.0-beta100");

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-beta1-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-beta9-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-beta100-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.1.9-beta105-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-beta10-full.nupkg"))
            };

            ReleaseEntry.WriteReleaseFile(releaseEntries, path);

            var releases = ReleaseEntry.ParseReleaseFile(File.ReadAllText(path)).ToArray();

            Assert.Equal(firstVersion, releases[0].Version);
            Assert.Equal(secondVersion, releases[2].Version);
            Assert.Equal(thirdVersion, releases[3].Version);
            Assert.Equal(fourthVersion, releases[4].Version);
        }

        [Fact]
        public void StagingUsersGetBetaSoftware()
        {
            // NB: We're kind of using a hack here, in that we know that the 
            // last 4 bytes are used as the percentage, and the percentage 
            // effectively measures, "How close are you to zero". Guid.Empty
            // is v close to zero, because it is zero.
            var path = Path.GetTempFileName();
            var ourGuid = Guid.Empty;

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-full.nupkg", 0.1f)),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.1.0-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.0.0-full.nupkg"))
            };

            ReleaseEntry.WriteReleaseFile(releaseEntries, path);

            var releases = ReleaseEntry.ParseReleaseFileAndApplyStaging(File.ReadAllText(path), ourGuid).ToArray();
            Assert.Equal(3, releases.Length);
        }

        [Fact]
        public void BorkedUsersGetProductionSoftware()
        {
            var path = Path.GetTempFileName();
            var ourGuid = default(Guid?);

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-full.nupkg", 0.1f)),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.1.0-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.0.0-full.nupkg"))
            };

            ReleaseEntry.WriteReleaseFile(releaseEntries, path);

            var releases = ReleaseEntry.ParseReleaseFileAndApplyStaging(File.ReadAllText(path), ourGuid).ToArray();
            Assert.Equal(2, releases.Length);
        }

        [Theory]
        [InlineData("{22b29e6f-bd2e-43d2-85ca-ffffffffffff}")]
        [InlineData("{22b29e6f-bd2e-43d2-85ca-888888888888}")]
        [InlineData("{22b29e6f-bd2e-43d2-85ca-444444444444}")]
        public void UnluckyUsersGetProductionSoftware(string inputGuid)
        {
            var path = Path.GetTempFileName();
            var ourGuid = Guid.ParseExact(inputGuid, "B");

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-full.nupkg", 0.1f)),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.1.0-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.0.0-full.nupkg"))
            };

            ReleaseEntry.WriteReleaseFile(releaseEntries, path);

            var releases = ReleaseEntry.ParseReleaseFileAndApplyStaging(File.ReadAllText(path), ourGuid).ToArray();
            Assert.Equal(2, releases.Length);
        }

        [Theory]
        [InlineData("{22b29e6f-bd2e-43d2-85ca-333333333333}")]
        [InlineData("{22b29e6f-bd2e-43d2-85ca-111111111111}")]
        [InlineData("{22b29e6f-bd2e-43d2-85ca-000000000000}")]
        public void LuckyUsersGetBetaSoftware(string inputGuid)
        {
            var path = Path.GetTempFileName();
            var ourGuid = Guid.ParseExact(inputGuid, "B");

            var releaseEntries = new[] {
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.2.0-full.nupkg", 0.25f)),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.1.0-full.nupkg")),
                ReleaseEntry.ParseReleaseEntry(MockReleaseEntry("Espera-1.0.0-full.nupkg"))
            };

            ReleaseEntry.WriteReleaseFile(releaseEntries, path);

            var releases = ReleaseEntry.ParseReleaseFileAndApplyStaging(File.ReadAllText(path), ourGuid).ToArray();
            Assert.Equal(3, releases.Length);
        }

        [Fact]
        public void ParseReleaseFileShouldReturnNothingForBlankFiles()
        {
            Assert.True(ReleaseEntry.ParseReleaseFile("").Count() == 0);
            Assert.True(ReleaseEntry.ParseReleaseFile(null).Count() == 0);
        }

        [Fact]
        public void JsonReleaseFileRoundTrip()
        {
            var entries = new[] {
                ReleaseEntry.ParseReleaseEntry("0000000000000000000000000000000000000000  Demo-1.2.0-full.nupkg  200"),
                ReleaseEntry.ParseReleaseEntry("0000000000000000000000000000000000000000  Demo-1.1.0-delta.nupkg  100"),
                ReleaseEntry.ParseReleaseEntry("0000000000000000000000000000000000000000  Demo-1.1.0-full.nupkg  150"),
            };
            var path = Path.GetTempFileName();

            try {
                ReleaseEntry.WriteReleaseFile(entries, path);

                var contents = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
                Assert.StartsWith("{", contents);

                var document = (IDictionary<string, object>)SimpleJson.DeserializeObject(contents);
                Assert.Equal(1L, document["formatVersion"]);
                var releases = (IList<object>)document["releases"];
                Assert.Equal(3, releases.Count);

                var first = (IDictionary<string, object>)releases[0];
                var second = (IDictionary<string, object>)releases[1];
                var third = (IDictionary<string, object>)releases[2];
                Assert.Equal("Demo-1.1.0-delta.nupkg", first["filename"]);
                Assert.Equal("Demo-1.1.0-full.nupkg", second["filename"]);
                Assert.Equal("Demo-1.2.0-full.nupkg", third["filename"]);
                Assert.Equal("0000000000000000000000000000000000000000", first["sha1"]);
                Assert.Equal(100L, first["filesize"]);
                Assert.False(first.ContainsKey("isDelta"));

                var parsed = ReleaseEntry.ParseReleaseFile(contents).ToArray();
                Assert.Equal(new[] { "Demo-1.1.0-delta.nupkg", "Demo-1.1.0-full.nupkg", "Demo-1.2.0-full.nupkg" }, parsed.Select(x => x.Filename).ToArray());
            } finally {
                File.Delete(path);
            }
        }

        [Fact]
        public void JsonReleaseEntryPreservesOptionsThroughRoundTrip()
        {
            var contents = @"{
                ""formatVersion"": 1,
                ""releases"": [{
                    ""sha1"": ""0000000000000000000000000000000000000000"",
                    ""filename"": ""Demo-1.2.0-full.nupkg"",
                    ""filesize"": 200,
                    ""baseUrl"": ""https://updates.example.test/releases/"",
                    ""query"": ""?channel=beta"",
                    ""stagingPercentage"": 0.25,
                    ""options"": {
                        ""forceUpdate"": true,
                        ""unknown"": { ""nested"": [""value"", 2] }
                    }
                }]
            }";

            var entry = ReleaseEntry.ParseReleaseFile(contents).Single();
            Assert.Equal("https://updates.example.test/releases/", entry.BaseUrl);
            Assert.Equal("?channel=beta", entry.Query);
            Assert.Equal(200L, entry.Filesize);
            Assert.Equal(0.25f, entry.StagingPercentage.Value, 3);
            Assert.True(entry.ForceUpdate);
            var unknown = (IDictionary<string, object>)entry.Options["unknown"];
            var unknownNested = (IList<object>)unknown["nested"];
            Assert.Equal("value", unknownNested[0]);

            var path = Path.GetTempFileName();
            try {
                ReleaseEntry.WriteReleaseFile(new[] { entry }, path);
                var roundTripped = ReleaseEntry.ParseReleaseFile(File.ReadAllText(path, Encoding.UTF8)).Single();
                Assert.Equal(entry.Filename, roundTripped.Filename);
                Assert.Equal(entry.SHA1, roundTripped.SHA1);
                Assert.Equal(entry.Filesize, roundTripped.Filesize);
                Assert.Equal(entry.BaseUrl, roundTripped.BaseUrl);
                Assert.Equal(entry.Query, roundTripped.Query);
                Assert.Equal(entry.StagingPercentage, roundTripped.StagingPercentage);
                Assert.True(roundTripped.ForceUpdate);
                var roundTrippedUnknown = (IDictionary<string, object>)roundTripped.Options["unknown"];
                var nested = (IList<object>)roundTrippedUnknown["nested"];
                Assert.Equal("value", nested[0]);
                Assert.Equal(2L, nested[1]);
            } finally {
                File.Delete(path);
            }
        }

        [Fact]
        public void LegacyReleaseFileStillParses()
        {
            var contents = "0000000000000000000000000000000000000000  Demo-1.0.0-full.nupkg  100\n" +
                "0000000000000000000000000000000000000000  Demo-1.1.0-full.nupkg  100 # 10%\n";

            Assert.Equal(2, ReleaseEntry.ParseReleaseFile(contents).Count());
            Assert.Equal(1, ReleaseEntry.ParseReleaseFileAndApplyStaging(contents, default(Guid?)).Count());
            Assert.Empty(ReleaseEntry.ParseReleaseFile(null));
            Assert.Empty(ReleaseEntry.ParseReleaseFile(" \r\n\t"));
        }

        [Fact]
        public void UnsupportedJsonReleaseFormatVersionThrowsSerializationException()
        {
            var ex = Assert.Throws<SerializationException>(() => ReleaseEntry.ParseReleaseFile("{\"formatVersion\":2,\"releases\":[]}"));
            Assert.Equal("Unsupported RELEASES format version: 2.", ex.Message);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"formatVersion\":1}")]
        [InlineData("{\"formatVersion\":1,\"releases\":null}")]
        [InlineData("{\"formatVersion\":1,\"releases\":[{\"sha1\":\"bad\",\"filename\":\"Demo-1.0.0-full.nupkg\",\"filesize\":1}]}")]
        [InlineData("{\"formatVersion\":1,\"releases\":[{\"sha1\":\"0000000000000000000000000000000000000000\",\"filename\":\"Demo-1.0.0-full.nupkg\",\"filesize\":0}]}")]
        public void InvalidJsonReleaseFilesThrowSerializationException(string contents)
        {
            Assert.Throws<SerializationException>(() => ReleaseEntry.ParseReleaseFile(contents));
        }

        static string MockReleaseEntry(string name, float? percentage = null)
        {
            if (percentage.HasValue) {
                var ret = String.Format("94689fede03fed7ab59c24337673a27837f0c3ec  {0}  1004502 # {1:F0}%", name, percentage * 100.0f);
                return ret;
            } else {
                return String.Format("94689fede03fed7ab59c24337673a27837f0c3ec  {0}  1004502", name);
            }
        }
    }
}
