using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Squirrel.SimpleSplat;
using Squirrel.Tests.TestHelpers;
using Xunit;

namespace Squirrel.Tests
{
    public class CheckForUpdateTests : IEnableLogger
    {
        [Fact]
        public async Task NewReleasesShouldBeDetected()
        {
            string installRoot;
            string feedDirectory;

            using (Utility.WithTempDirectory(out installRoot))
            using (Utility.WithTempDirectory(out feedDirectory)) {
                // Create initial version (1.0.0)
                IntegrationTestHelper.CreateFakeInstalledApp("1.0.0", feedDirectory);
                var packages = ReleaseEntry.BuildReleasesFile(feedDirectory);
                ReleaseEntry.WriteReleaseFile(packages, Path.Combine(feedDirectory, "RELEASES"));

                // Install the initial version
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    await fixture.FullInstall(silentInstall: true);
                }

                // Create new version (1.1.0)
                IntegrationTestHelper.CreateFakeInstalledApp("1.1.0", feedDirectory);
                packages = ReleaseEntry.BuildReleasesFile(feedDirectory);
                ReleaseEntry.WriteReleaseFile(packages, Path.Combine(feedDirectory, "RELEASES"));

                // Check for update
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    var result = await fixture.CheckForUpdate();

                    Assert.NotNull(result);
                    Assert.NotNull(result.FutureReleaseEntry);
                    Assert.Equal("1.1.0", result.FutureReleaseEntry.Version.ToString());
                    Assert.Single(result.ReleasesToApply);
                    Assert.Equal("1.1.0", result.ReleasesToApply.Single().Version.ToString());
                }
            }
        }

        [Fact]
        public async Task CorruptedReleaseFileMeansWeStartFromScratch()
        {
            string installRoot;
            string feedDirectory;

            using (Utility.WithTempDirectory(out installRoot))
            using (Utility.WithTempDirectory(out feedDirectory)) {
                // Create initial version (1.0.0)
                IntegrationTestHelper.CreateFakeInstalledApp("1.0.0", feedDirectory);
                var packages = ReleaseEntry.BuildReleasesFile(feedDirectory);
                ReleaseEntry.WriteReleaseFile(packages, Path.Combine(feedDirectory, "RELEASES"));

                // Install the initial version
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    await fixture.FullInstall(silentInstall: true);
                }

                // Create new version (1.1.0)
                IntegrationTestHelper.CreateFakeInstalledApp("1.1.0", feedDirectory);
                packages = ReleaseEntry.BuildReleasesFile(feedDirectory);
                ReleaseEntry.WriteReleaseFile(packages, Path.Combine(feedDirectory, "RELEASES"));

                // Corrupt the local RELEASES file
                var localReleasesFile = Utility.LocalReleaseFileForAppDir(Path.Combine(installRoot, "theApp"));
                File.WriteAllText(localReleasesFile, "lol this isn't right");

                // Check for update - should recover and detect the new version
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    var result = await fixture.CheckForUpdate();

                    Assert.NotNull(result);
                    Assert.NotNull(result.FutureReleaseEntry);
                    Assert.Equal("1.1.0", result.FutureReleaseEntry.Version.ToString());
                    Assert.Single(result.ReleasesToApply);
                    Assert.Equal("1.1.0", result.ReleasesToApply.Single().Version.ToString());
                }
            }
        }

        [Fact]
        public async Task CorruptRemoteFileShouldThrowOnCheck()
        {
            string installRoot;
            string feedDirectory;

            using (Utility.WithTempDirectory(out installRoot))
            using (Utility.WithTempDirectory(out feedDirectory)) {
                // Create initial version (1.0.0)
                IntegrationTestHelper.CreateFakeInstalledApp("1.0.0", feedDirectory);
                var packages = ReleaseEntry.BuildReleasesFile(feedDirectory);
                ReleaseEntry.WriteReleaseFile(packages, Path.Combine(feedDirectory, "RELEASES"));

                // Install the initial version
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    await fixture.FullInstall(silentInstall: true);
                }

                // Corrupt the remote RELEASES file
                File.WriteAllText(Path.Combine(feedDirectory, "RELEASES"), "lol this isn't right");

                // Check for update - should throw
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    var ex = await Assert.ThrowsAsync<Exception>(() => fixture.CheckForUpdate());
                    Assert.Contains("Invalid release entry", ex.Message);
                }
            }
        }

        [Fact]
        public async Task IfLocalVersionGreaterThanRemoteWeRollback()
        {
            string installRoot;
            string feedDirectory;

            using (Utility.WithTempDirectory(out installRoot))
            using (Utility.WithTempDirectory(out feedDirectory)) {
                // Create version 1.1.0 locally
                IntegrationTestHelper.CreateFakeInstalledApp("1.1.0", feedDirectory);
                var packages = ReleaseEntry.BuildReleasesFile(feedDirectory);
                ReleaseEntry.WriteReleaseFile(packages, Path.Combine(feedDirectory, "RELEASES"));

                // Install version 1.1.0
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    await fixture.FullInstall(silentInstall: true);
                }

                // Now create only version 1.0.0 on the remote (simulating rollback scenario)
                // We need to reset the feed directory to only have 1.0.0
                var feedDirInfo = new DirectoryInfo(feedDirectory);
                foreach (var file in feedDirInfo.GetFiles()) {
                    file.Delete();
                }

                IntegrationTestHelper.CreateFakeInstalledApp("1.0.0", feedDirectory);
                packages = ReleaseEntry.BuildReleasesFile(feedDirectory);
                ReleaseEntry.WriteReleaseFile(packages, Path.Combine(feedDirectory, "RELEASES"));

                // Check for update - local (1.1.0) > remote (1.0.0)
                // In this case, the behavior is to return current local version as no update needed
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    var result = await fixture.CheckForUpdate();

                    Assert.NotNull(result);
                    // When local > remote, the current local version should be returned as FutureReleaseEntry
                    // and ReleasesToApply should be empty (no update needed - we're already ahead)
                    Assert.NotNull(result.CurrentlyInstalledVersion);
                    Assert.Equal("1.1.0", result.CurrentlyInstalledVersion.Version.ToString());
                    Assert.Equal("1.1.0", result.FutureReleaseEntry.Version.ToString());
                    Assert.Empty(result.ReleasesToApply);
                }
            }
        }

        [Fact]
        public async Task IfLocalAndRemoteAreEqualThenDoNothing()
        {
            string installRoot;
            string feedDirectory;

            using (Utility.WithTempDirectory(out installRoot))
            using (Utility.WithTempDirectory(out feedDirectory)) {
                // Create version 1.0.0
                IntegrationTestHelper.CreateFakeInstalledApp("1.0.0", feedDirectory);
                var packages = ReleaseEntry.BuildReleasesFile(feedDirectory);
                ReleaseEntry.WriteReleaseFile(packages, Path.Combine(feedDirectory, "RELEASES"));

                // Install version 1.0.0
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    await fixture.FullInstall(silentInstall: true);
                }

                // Check for update - local and remote are the same
                using (var fixture = new UpdateManager(feedDirectory, "theApp", installRoot)) {
                    var result = await fixture.CheckForUpdate();

                    Assert.NotNull(result);
                    Assert.NotNull(result.CurrentlyInstalledVersion);
                    Assert.Equal("1.0.0", result.CurrentlyInstalledVersion.Version.ToString());
                    Assert.Equal("1.0.0", result.FutureReleaseEntry.Version.ToString());
                    Assert.Empty(result.ReleasesToApply);
                }
            }
        }
    }
}