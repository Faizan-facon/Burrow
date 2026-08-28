using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet;
using Squirrel;
using Squirrel.Testing;
using Xunit;

namespace Squirrel.Tests
{
    public class FakeUpdateManagerTests
    {
        [Fact]
        public async Task InstallsFromNoCurrentVersion()
        {
            using (var manager = new FakeUpdateManager()) {
                manager.PublishRelease(new SemanticVersion("1.0.0"));

                var updateInfo = await manager.CheckForUpdate(intention: UpdaterIntention.Install);

                Assert.Null(updateInfo.CurrentlyInstalledVersion);
                Assert.Equal("1.0.0", updateInfo.FutureReleaseEntry.Version.ToString());
                Assert.Single(updateInfo.ReleasesToApply);
            }
        }

        [Fact]
        public async Task UpdatesFromAnInstalledVersion()
        {
            using (var manager = new FakeUpdateManager(initialVersion: new SemanticVersion("1.0.0"))) {
                manager.PublishRelease(new SemanticVersion("1.1.0"));

                var updateInfo = await manager.CheckForUpdate();
                var path = await manager.ApplyReleases(updateInfo);

                Assert.Equal("1.1.0", updateInfo.FutureReleaseEntry.Version.ToString());
                Assert.Equal("1.1.0", manager.CurrentVersion.ToString());
                Assert.True(manager.IsInstalled);
                Assert.Equal(manager.RootAppDirectory + "\\app-1.1.0", path);
            }
        }

        [Fact]
        public async Task ReturnsNoReleasesWhenNewestReleaseIsInstalled()
        {
            using (var manager = new FakeUpdateManager(initialVersion: new SemanticVersion("1.0.0"))) {
                manager.PublishRelease(new SemanticVersion("1.0.0"));

                var updateInfo = await manager.CheckForUpdate();

                Assert.Empty(updateInfo.ReleasesToApply);
                Assert.Equal("1.0.0", updateInfo.FutureReleaseEntry.Version.ToString());
            }
        }

        [Fact]
        public async Task UpdateAppRecordsConsumerOrchestration()
        {
            using (var manager = new FakeUpdateManager(initialVersion: new SemanticVersion("1.0.0"))) {
                manager.PublishRelease(new SemanticVersion("1.1.0"));

                var release = await new AppUpdater(manager).UpdateAsync();

                Assert.Equal("1.1.0", release.Version.ToString());
                Assert.Equal("1.1.0", manager.CurrentVersion.ToString());
                Assert.True(manager.IsUninstallerRegistered);
                Assert.Equal(
                    new[] {
                        FakeUpdateOperation.CheckForUpdate,
                        FakeUpdateOperation.DownloadReleases,
                        FakeUpdateOperation.ApplyReleases,
                        FakeUpdateOperation.CreateUninstallerRegistryEntry,
                    },
                    manager.Calls.Select(x => x.Operation).ToArray());
                Assert.Equal("1.1.0", manager.Calls[2].Version.ToString());
            }
        }

        [Fact]
        public async Task FullInstallRecordsSilentInstallAndChangesState()
        {
            using (var manager = new FakeUpdateManager()) {
                manager.PublishRelease(new SemanticVersion("1.0.0"));

                await manager.FullInstall(silentInstall: true);

                Assert.True(manager.IsInstalled);
                Assert.Equal("1.0.0", manager.CurrentVersion.ToString());
                Assert.Equal(FakeUpdateOperation.FullInstall, manager.Calls[0].Operation);
                Assert.True(manager.Calls[0].SilentInstall);
                Assert.Equal(FakeUpdateOperation.ApplyReleases, manager.Calls[3].Operation);
            }
        }

        [Fact]
        public async Task FullUninstallClearsAllSyntheticState()
        {
            using (var manager = new FakeUpdateManager(initialVersion: new SemanticVersion("1.0.0"))) {
                manager.PublishRelease(new SemanticVersion("1.1.0"));
                await manager.UpdateApp();
                manager.CreateShortcutsForExecutable("MyApp.exe", ShortcutLocation.Desktop | ShortcutLocation.StartMenu, false, "--test", "icon.ico");

                await manager.FullUninstall();

                Assert.False(manager.IsInstalled);
                Assert.Null(manager.CurrentVersion);
                Assert.False(manager.IsUninstallerRegistered);
                Assert.Empty(manager.Shortcuts);
                Assert.Equal(FakeUpdateOperation.FullUninstall, manager.Calls.Last().Operation);
            }
        }

        [Fact]
        public async Task ReportsProgressForSuccessfulOperations()
        {
            using (var manager = new FakeUpdateManager(initialVersion: new SemanticVersion("1.0.0"))) {
                manager.PublishRelease(new SemanticVersion("1.1.0"));
                var progress = new List<int>();

                var updateInfo = await manager.CheckForUpdate(progress: progress.Add);
                await manager.DownloadReleases(updateInfo.ReleasesToApply, progress.Add);
                await manager.ApplyReleases(updateInfo, progress.Add);

                Assert.Equal(new[] { 0, 100, 0, 100, 0, 100 }, progress.ToArray());
            }
        }

        [Fact]
        public async Task ReportsCurrentVersionAndRegistryTransitions()
        {
            using (var manager = new FakeUpdateManager(initialVersion: new SemanticVersion("1.0.0"))) {
                Assert.Equal("1.0.0", manager.CurrentlyInstalledVersion().ToString());
                await manager.CreateUninstallerRegistryEntry("update.exe --uninstall", "--silent");
                Assert.True(manager.IsUninstallerRegistered);
                Assert.Equal("update.exe --uninstall", manager.Calls[1].UninstallCommand);
                Assert.Equal("--silent", manager.Calls[1].QuietSwitch);

                manager.RemoveUninstallerRegistryEntry();

                Assert.False(manager.IsUninstallerRegistered);
                Assert.Equal(FakeUpdateOperation.RemoveUninstallerRegistryEntry, manager.Calls[2].Operation);
            }
        }

        [Fact]
        public void TracksShortcutFlagsAndCallMetadata()
        {
            using (var manager = new FakeUpdateManager()) {
                manager.CreateShortcutsForExecutable("MyApp.exe", ShortcutLocation.Desktop | ShortcutLocation.StartMenu, false, "--test", "app.ico");
                manager.CreateShortcutsForExecutable("MyApp.exe", ShortcutLocation.Startup, true);

                Assert.Equal(ShortcutLocation.Desktop | ShortcutLocation.StartMenu | ShortcutLocation.Startup, manager.Shortcuts["MyApp.exe"]);
                Assert.False(manager.Calls[0].UpdateOnly);
                Assert.Equal("--test", manager.Calls[0].ProgramArguments);
                Assert.Equal("app.ico", manager.Calls[0].Icon);
                Assert.True(manager.Calls[1].UpdateOnly);

                manager.RemoveShortcutsForExecutable("MyApp.exe", ShortcutLocation.StartMenu | ShortcutLocation.Startup);
                Assert.Equal(ShortcutLocation.Desktop, manager.Shortcuts["MyApp.exe"]);
                manager.RemoveShortcutsForExecutable("MyApp.exe", ShortcutLocation.Desktop);
                Assert.Empty(manager.Shortcuts);
            }
        }

        [Fact]
        public void RejectsInvalidPublicationInput()
        {
            Assert.Throws<ArgumentException>(() => new FakeUpdateManager(null));
            Assert.Throws<ArgumentException>(() => new FakeUpdateManager(String.Empty));

            using (var manager = new FakeUpdateManager()) {
                Assert.Throws<ArgumentException>(() => manager.PublishRelease(null));
                manager.PublishRelease(new SemanticVersion("1.0.0"));
                Assert.Throws<ArgumentException>(() => manager.PublishRelease(new SemanticVersion("1.0.0")));
                Assert.Throws<ArgumentNullException>(() => manager.Fail(FakeUpdateOperation.ApplyReleases, null));
                Assert.Throws<ArgumentNullException>(() => manager.FailNext(FakeUpdateOperation.ApplyReleases, null));
            }
        }

        [Fact]
        public async Task RejectsUnknownAndMissingDownloadReleases()
        {
            using (var manager = new FakeUpdateManager()) {
                manager.PublishRelease(new SemanticVersion("1.0.0"));
                var updateInfo = await manager.CheckForUpdate(intention: UpdaterIntention.Install);

                await Assert.ThrowsAsync<ArgumentNullException>(() => manager.DownloadReleases(null));
                await Assert.ThrowsAsync<ArgumentException>(() => manager.DownloadReleases(new[] {
                    ReleaseEntry.ParseReleaseEntry("0000000000000000000000000000000000000000 OtherApp-1.0.0-full.nupkg 1")
                }));
                await manager.DownloadReleases(updateInfo.ReleasesToApply);
            }
        }

        [Fact]
        public async Task QueuedFailureIsConsumedOnceForEveryManagerOperation()
        {
            using (var manager = new FakeUpdateManager(initialVersion: new SemanticVersion("1.0.0"))) {
                manager.PublishRelease(new SemanticVersion("1.1.0"));
                var expected = new InvalidOperationException("queued");
                var updateInfo = await manager.CheckForUpdate();

                manager.FailNext(FakeUpdateOperation.CheckForUpdate, expected);
                await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CheckForUpdate());
                await manager.CheckForUpdate();

                manager.FailNext(FakeUpdateOperation.DownloadReleases, expected);
                await Assert.ThrowsAsync<InvalidOperationException>(() => manager.DownloadReleases(updateInfo.ReleasesToApply));
                await manager.DownloadReleases(updateInfo.ReleasesToApply);

                manager.FailNext(FakeUpdateOperation.ApplyReleases, expected);
                await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ApplyReleases(updateInfo));
                await manager.ApplyReleases(updateInfo);

                manager.FailNext(FakeUpdateOperation.FullInstall, expected);
                await Assert.ThrowsAsync<InvalidOperationException>(() => manager.FullInstall());

                manager.FailNext(FakeUpdateOperation.FullUninstall, expected);
                await Assert.ThrowsAsync<InvalidOperationException>(() => manager.FullUninstall());
                await manager.FullUninstall();

                manager.FailNext(FakeUpdateOperation.CurrentlyInstalledVersion, expected);
                Assert.Throws<InvalidOperationException>(() => manager.CurrentlyInstalledVersion());
                Assert.Null(manager.CurrentlyInstalledVersion());

                manager.FailNext(FakeUpdateOperation.CreateUninstallerRegistryEntry, expected);
                await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CreateUninstallerRegistryEntry());
                await manager.CreateUninstallerRegistryEntry();

                manager.FailNext(FakeUpdateOperation.RemoveUninstallerRegistryEntry, expected);
                Assert.Throws<InvalidOperationException>(() => manager.RemoveUninstallerRegistryEntry());
                manager.RemoveUninstallerRegistryEntry();

                manager.FailNext(FakeUpdateOperation.CreateShortcutsForExecutable, expected);
                Assert.Throws<InvalidOperationException>(() => manager.CreateShortcutsForExecutable("MyApp.exe", ShortcutLocation.Desktop, false));
                manager.CreateShortcutsForExecutable("MyApp.exe", ShortcutLocation.Desktop, false);

                manager.FailNext(FakeUpdateOperation.RemoveShortcutsForExecutable, expected);
                Assert.Throws<InvalidOperationException>(() => manager.RemoveShortcutsForExecutable("MyApp.exe", ShortcutLocation.Desktop));
                manager.RemoveShortcutsForExecutable("MyApp.exe", ShortcutLocation.Desktop);
            }
        }

        [Fact]
        public async Task PersistentFailureWinsUntilCleared()
        {
            using (var manager = new FakeUpdateManager()) {
                var expected = new InvalidOperationException("offline");
                manager.PublishRelease(new SemanticVersion("1.0.0"));
                manager.Fail(FakeUpdateOperation.CheckForUpdate, expected);
                manager.FailNext(FakeUpdateOperation.CheckForUpdate, new Exception("queued"));

                var first = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CheckForUpdate());
                Assert.Same(expected, first);
                manager.ClearFailure(FakeUpdateOperation.CheckForUpdate);
                var second = await Assert.ThrowsAsync<Exception>(() => manager.CheckForUpdate());
                Assert.Equal("queued", second.Message);
                await manager.CheckForUpdate();
            }
        }
    }

    sealed class AppUpdater
    {
        readonly IUpdateManager manager;

        public AppUpdater(IUpdateManager manager)
        {
            this.manager = manager;
        }

        public Task<ReleaseEntry> UpdateAsync()
        {
            return manager.UpdateApp();
        }
    }
}
