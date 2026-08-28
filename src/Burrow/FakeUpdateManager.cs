using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using NuGet;
using Squirrel;
using Squirrel.SimpleSplat;

namespace Squirrel.Testing
{
    public enum FakeUpdateOperation
    {
        CheckForUpdate,
        DownloadReleases,
        ApplyReleases,
        FullInstall,
        FullUninstall,
        CurrentlyInstalledVersion,
        CreateUninstallerRegistryEntry,
        RemoveUninstallerRegistryEntry,
        CreateShortcutsForExecutable,
        RemoveShortcutsForExecutable
    }

    public sealed class FakeUpdateCall
    {
        public FakeUpdateOperation Operation { get; }
        public SemanticVersion Version { get; }
        public IReadOnlyList<ReleaseEntry> Releases { get; }
        public bool SilentInstall { get; }
        public string ExecutableName { get; }
        public ShortcutLocation Locations { get; }
        public bool UpdateOnly { get; }
        public string ProgramArguments { get; }
        public string Icon { get; }
        public string UninstallCommand { get; }
        public string QuietSwitch { get; }

        internal FakeUpdateCall(
            FakeUpdateOperation operation,
            SemanticVersion version = null,
            IEnumerable<ReleaseEntry> releases = null,
            bool silentInstall = false,
            string executableName = null,
            ShortcutLocation locations = 0,
            bool updateOnly = false,
            string programArguments = null,
            string icon = null,
            string uninstallCommand = null,
            string quietSwitch = null)
        {
            Operation = operation;
            Version = version;
            Releases = new List<ReleaseEntry>(releases ?? Enumerable.Empty<ReleaseEntry>()).AsReadOnly();
            SilentInstall = silentInstall;
            ExecutableName = executableName;
            Locations = locations;
            UpdateOnly = updateOnly;
            ProgramArguments = programArguments;
            Icon = icon;
            UninstallCommand = uninstallCommand;
            QuietSwitch = quietSwitch;
        }
    }

    public sealed class FakeUpdateManager : IUpdateManager
    {
        readonly string applicationName;
        readonly string rootAppDirectory;
        readonly List<ReleaseEntry> publishedReleases = new List<ReleaseEntry>();
        readonly List<FakeUpdateCall> calls = new List<FakeUpdateCall>();
        readonly Dictionary<string, ShortcutLocation> shortcuts = new Dictionary<string, ShortcutLocation>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<FakeUpdateOperation, Exception> persistentFailures = new Dictionary<FakeUpdateOperation, Exception>();
        readonly Dictionary<FakeUpdateOperation, Queue<Exception>> queuedFailures = new Dictionary<FakeUpdateOperation, Queue<Exception>>();
        ReleaseEntry currentRelease;

        public FakeUpdateManager(string applicationName = "FakeApp", string rootDirectory = null, SemanticVersion initialVersion = null)
        {
            if (String.IsNullOrEmpty(applicationName)) {
                throw new ArgumentException("Application name cannot be null or empty.", nameof(applicationName));
            }

            this.applicationName = applicationName;
            rootAppDirectory = Path.Combine(rootDirectory ?? "fake-root", applicationName);

            if (initialVersion != null) {
                currentRelease = createRelease(initialVersion);
                CurrentVersion = initialVersion;
                IsInstalled = true;
            }
        }

        public string ApplicationName { get { return applicationName; } }
        public string RootAppDirectory { get { return rootAppDirectory; } }
        public SemanticVersion CurrentVersion { get; private set; }
        public bool IsInstalled { get; private set; }
        public bool IsUninstallerRegistered { get; private set; }

        public IReadOnlyList<FakeUpdateCall> Calls {
            get { return calls.AsReadOnly(); }
        }

        public IReadOnlyDictionary<string, ShortcutLocation> Shortcuts
        {
            get { return new ReadOnlyDictionary<string, ShortcutLocation>(new Dictionary<string, ShortcutLocation>(shortcuts, StringComparer.OrdinalIgnoreCase)); }
        }

        public void PublishRelease(SemanticVersion version)
        {
            if (version == null) {
                throw new ArgumentException("Release version cannot be null.", nameof(version));
            }

            if (publishedReleases.Any(x => x.Version.Equals(version))) {
                throw new ArgumentException("A release with this version has already been published.", nameof(version));
            }

            publishedReleases.Add(createRelease(version));
        }

        public void Fail(FakeUpdateOperation operation, Exception exception)
        {
            if (exception == null) {
                throw new ArgumentNullException(nameof(exception));
            }

            persistentFailures[operation] = exception;
        }

        public void ClearFailure(FakeUpdateOperation operation)
        {
            persistentFailures.Remove(operation);
        }

        public void FailNext(FakeUpdateOperation operation, Exception exception)
        {
            if (exception == null) {
                throw new ArgumentNullException(nameof(exception));
            }

            Queue<Exception> failures;
            if (!queuedFailures.TryGetValue(operation, out failures)) {
                failures = new Queue<Exception>();
                queuedFailures[operation] = failures;
            }

            failures.Enqueue(exception);
        }

        public async Task<UpdateInfo> CheckForUpdate(bool ignoreDeltaUpdates = false, Action<int> progress = null, UpdaterIntention intention = UpdaterIntention.Update)
        {
            record(new FakeUpdateCall(FakeUpdateOperation.CheckForUpdate));
            consumeFailure(FakeUpdateOperation.CheckForUpdate);

            if (publishedReleases.Count == 0) {
                throw new InvalidOperationException("No releases have been published.");
            }

            var current = intention == UpdaterIntention.Install ? null : currentRelease;
            reportProgress(progress);
            return await Task.FromResult(UpdateInfo.Create(current, publishedReleases, "fake"));
        }

        public async Task DownloadReleases(IEnumerable<ReleaseEntry> releasesToDownload, Action<int> progress = null)
        {
            var releases = releasesToDownload == null
                ? new List<ReleaseEntry>()
                : releasesToDownload.ToList();
            record(new FakeUpdateCall(FakeUpdateOperation.DownloadReleases, releases: releases));
            consumeFailure(FakeUpdateOperation.DownloadReleases);

            if (releasesToDownload == null) {
                throw new ArgumentNullException(nameof(releasesToDownload));
            }

            if (releases.Any(x => x == null || !publishedReleases.Contains(x))) {
                throw new ArgumentException("The requested release is not published by this manager.", nameof(releasesToDownload));
            }

            reportProgress(progress);
            await Task.CompletedTask;
        }

        public async Task<string> ApplyReleases(UpdateInfo updateInfo, Action<int> progress = null)
        {
            var releases = updateInfo == null || updateInfo.ReleasesToApply == null
                ? new List<ReleaseEntry>()
                : updateInfo.ReleasesToApply.ToList();
            var version = updateInfo == null ? null : updateInfo.FutureReleaseEntry?.Version;
            record(new FakeUpdateCall(FakeUpdateOperation.ApplyReleases, version, releases));
            consumeFailure(FakeUpdateOperation.ApplyReleases);

            if (updateInfo == null) {
                throw new ArgumentNullException(nameof(updateInfo));
            }

            if (releases.Any()) {
                if (updateInfo.FutureReleaseEntry == null) {
                    throw new ArgumentException("An update with releases must have a future release.", nameof(updateInfo));
                }

                CurrentVersion = updateInfo.FutureReleaseEntry.Version;
                currentRelease = createRelease(CurrentVersion);
                IsInstalled = true;
            }

            reportProgress(progress);
            await Task.CompletedTask;
            return Path.Combine(RootAppDirectory, "app-" + CurrentVersion);
        }

        public async Task FullInstall(bool silentInstall = false, Action<int> progress = null)
        {
            record(new FakeUpdateCall(FakeUpdateOperation.FullInstall, silentInstall: silentInstall));
            consumeFailure(FakeUpdateOperation.FullInstall);

            var updateInfo = await CheckForUpdate(intention: UpdaterIntention.Install);
            await DownloadReleases(updateInfo.ReleasesToApply);
            await ApplyReleases(updateInfo, progress);
        }

        public async Task FullUninstall()
        {
            record(new FakeUpdateCall(FakeUpdateOperation.FullUninstall));
            consumeFailure(FakeUpdateOperation.FullUninstall);

            currentRelease = null;
            CurrentVersion = null;
            IsInstalled = false;
            IsUninstallerRegistered = false;
            shortcuts.Clear();
            await Task.CompletedTask;
        }

        public SemanticVersion CurrentlyInstalledVersion(string executable = null)
        {
            record(new FakeUpdateCall(FakeUpdateOperation.CurrentlyInstalledVersion, CurrentVersion, executableName: executable));
            consumeFailure(FakeUpdateOperation.CurrentlyInstalledVersion);
            return IsInstalled ? CurrentVersion : null;
        }

        public async Task<RegistryKey> CreateUninstallerRegistryEntry(string uninstallCmd, string quietSwitch)
        {
            record(new FakeUpdateCall(
                FakeUpdateOperation.CreateUninstallerRegistryEntry,
                uninstallCommand: uninstallCmd,
                quietSwitch: quietSwitch));
            consumeFailure(FakeUpdateOperation.CreateUninstallerRegistryEntry);

            IsUninstallerRegistered = true;
            await Task.CompletedTask;
            return null;
        }

        public async Task<RegistryKey> CreateUninstallerRegistryEntry()
        {
            record(new FakeUpdateCall(FakeUpdateOperation.CreateUninstallerRegistryEntry));
            consumeFailure(FakeUpdateOperation.CreateUninstallerRegistryEntry);

            IsUninstallerRegistered = true;
            await Task.CompletedTask;
            return null;
        }

        public void RemoveUninstallerRegistryEntry()
        {
            record(new FakeUpdateCall(FakeUpdateOperation.RemoveUninstallerRegistryEntry));
            consumeFailure(FakeUpdateOperation.RemoveUninstallerRegistryEntry);
            IsUninstallerRegistered = false;
        }

        public void CreateShortcutsForExecutable(string exeName, ShortcutLocation locations, bool updateOnly, string programArguments = null, string icon = null)
        {
            record(new FakeUpdateCall(
                FakeUpdateOperation.CreateShortcutsForExecutable,
                executableName: exeName,
                locations: locations,
                updateOnly: updateOnly,
                programArguments: programArguments,
                icon: icon));
            consumeFailure(FakeUpdateOperation.CreateShortcutsForExecutable);

            ShortcutLocation existing;
            shortcuts.TryGetValue(exeName, out existing);
            shortcuts[exeName] = existing | locations;
        }

        public void RemoveShortcutsForExecutable(string exeName, ShortcutLocation locations)
        {
            record(new FakeUpdateCall(
                FakeUpdateOperation.RemoveShortcutsForExecutable,
                executableName: exeName,
                locations: locations));
            consumeFailure(FakeUpdateOperation.RemoveShortcutsForExecutable);

            ShortcutLocation existing;
            if (!shortcuts.TryGetValue(exeName, out existing)) {
                return;
            }

            var remaining = existing & ~locations;
            if (remaining == 0) {
                shortcuts.Remove(exeName);
            } else {
                shortcuts[exeName] = remaining;
            }
        }

        public void Dispose()
        {
        }

        void record(FakeUpdateCall call)
        {
            calls.Add(call);
        }

        void consumeFailure(FakeUpdateOperation operation)
        {
            Exception exception;
            if (persistentFailures.TryGetValue(operation, out exception)) {
                throw exception;
            }

            Queue<Exception> failures;
            if (!queuedFailures.TryGetValue(operation, out failures) || failures.Count == 0) {
                return;
            }

            exception = failures.Dequeue();
            if (failures.Count == 0) {
                queuedFailures.Remove(operation);
            }

            throw exception;
        }

        static void reportProgress(Action<int> progress)
        {
            if (progress == null) {
                return;
            }

            progress(0);
            progress(100);
        }

        ReleaseEntry createRelease(SemanticVersion version)
        {
            var filename = String.Format("{0}-{1}-full.nupkg", ApplicationName, version);
            return ReleaseEntry.ParseReleaseEntry(String.Format("{0} {1} 1", new string('0', 40), filename));
        }
    }
}
