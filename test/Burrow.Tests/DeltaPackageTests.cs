using NuGet;
using Squirrel;
using Squirrel.SimpleSplat;
using Squirrel.Tests.TestHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Squirrel.Tests.Core
{
    public class ApplyDeltaPackageTests : IEnableLogger
    {
        [Fact]
        public void ApplyDeltaPackageSmokeTest()
        {
            var basePackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.0.0.0-full.nupkg"));
            var deltaPackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.1.0.0-delta.nupkg"));
            var expectedPackageFile = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.1.0.0-full.nupkg");
            var outFile = Path.GetTempFileName() + ".nupkg";

            try {
                var deltaBuilder = new DeltaPackageBuilder();
                deltaBuilder.ApplyDeltaPackage(basePackage, deltaPackage, outFile);

                var result = new ZipPackage(outFile);
                var expected = new ZipPackage(expectedPackageFile);

                result.Id.ShouldEqual(expected.Id);
                result.Version.ShouldEqual(expected.Version);

                this.Log().Info("Expected file list:");
                var expectedList = expected.GetFiles().Select(x => x.Path).OrderBy(x => x).ToList();
                expectedList.ForEach(x => this.Log().Info(x));

                this.Log().Info("Actual file list:");
                var actualList = result.GetFiles().Select(x => x.Path).OrderBy(x => x).ToList();
                actualList.ForEach(x => this.Log().Info(x));

                Enumerable.Zip(expectedList, actualList, (e, a) => e == a)
                    .All(x => x != false)
                    .ShouldBeTrue();
            } finally {
                if (File.Exists(outFile)) {
                    File.Delete(outFile);
                }
            }
        }

        [Fact]
        public void ApplyDeltaWithBothBsdiffAndNormalDiffDoesntFail()
        {
            var basePackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "slack-1.1.8-full.nupkg"));
            var deltaPackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "slack-1.2.0-delta.nupkg"));
            var outFile = Path.GetTempFileName() + ".nupkg";

            try {
                var deltaBuilder = new DeltaPackageBuilder();
                deltaBuilder.ApplyDeltaPackage(basePackage, deltaPackage, outFile);

                var result = new ZipPackage(outFile);

                result.Id.ShouldEqual("slack");
                result.Version.ShouldEqual(new SemanticVersion("1.2.0"));
            } finally {
                if (File.Exists(outFile)) {
                    File.Delete(outFile);
                }
            }
        }

        [Fact]
        public void DiagnoseFailingBsdiffEntries()
        {
            var basePackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "slack-1.1.8-full.nupkg"));
            var deltaPackage = new ReleasePackage(IntegrationTestHelper.GetPath("fixtures", "slack-1.2.0-delta.nupkg"));

            Utility.WithTempDirectory(out var basePath, null);
            Utility.WithTempDirectory(out var deltaPath, null);

            ExtractZip(basePackage.InputPackageFile, basePath);
            ExtractZip(deltaPackage.InputPackageFile, deltaPath);

            var diagDir = Path.Combine(Path.GetTempPath(), "bsdiff_diag_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(diagDir);
            Console.WriteLine($"Diagnostic dir: {diagDir}");

            var bsdiffFiles = new DirectoryInfo(deltaPath).GetFiles("*.bsdiff", SearchOption.AllDirectories);
            Console.WriteLine($"Found {bsdiffFiles.Length} .bsdiff entries");

            var failures = new List<string>();

            foreach (var bsdiffFile in bsdiffFiles)
            {
                var relativePath = bsdiffFile.FullName.Substring(deltaPath.Length).TrimStart(Path.DirectorySeparatorChar);
                var oldRelative = Regex.Replace(relativePath, @"\.bsdiff$", "");
                var oldFilePath = Path.Combine(basePath, oldRelative);

                if (!File.Exists(oldFilePath))
                {
                    Console.WriteLine($"[SKIP] {relativePath} -- no matching base file at {oldFilePath}");
                    continue;
                }

                try
                {
                    using (var inf = File.OpenRead(oldFilePath))
                        using (var of = new MemoryStream())
                    {
                    Squirrel.Bsdiff.BinaryPatchUtility.Apply(inf, () => File.OpenRead(bsdiffFile.FullName), of);
                    Console.WriteLine($"[OK]   {relativePath} ({bsdiffFile.Length} byte patch, {new FileInfo(oldFilePath).Length} -> {of.Length} bytes)");

                    }
                }
                catch (Exception ex)
                {
                    failures.Add(relativePath);
                    Console.WriteLine($"[FAIL] {relativePath}: {ex.GetType().Name}: {ex.Message}");

                    var dumpPath = Path.Combine(diagDir, Path.GetFileName(bsdiffFile.FullName));
                    File.Copy(bsdiffFile.FullName, dumpPath, true);
                    File.Copy(oldFilePath, dumpPath + ".oldfile", true);
                    Console.WriteLine($"  patch copied to:    {dumpPath}");
                    Console.WriteLine($"  old file copied to: {dumpPath}.oldfile");

                    var patchBytes = File.ReadAllBytes(bsdiffFile.FullName);
                    long signature = ReadInt64(patchBytes, 0);
                    long controlLength = ReadInt64(patchBytes, 8);
                    long diffLength = ReadInt64(patchBytes, 16);
                    long newSize = ReadInt64(patchBytes, 24);
                    Console.WriteLine($"  signature=0x{signature:X16} controlLength={controlLength} " +
                                       $"diffLength={diffLength} newSize={newSize} totalPatchLen={patchBytes.Length}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {bsdiffFiles.Length}, failed: {failures.Count}");
            foreach (var f in failures) Console.WriteLine($"  - {f}");

            Assert.Empty(failures); // fails the test with the summary above still printed
        }

        static void ExtractZip(string zipPath, string destDir)
        {
            using (var za = ZipFile.OpenRead(zipPath))
                foreach (var entry in za.Entries)
                {
                    var targetFile = Path.Combine(destDir, entry.FullName);
                    var dir = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    if (!string.IsNullOrEmpty(entry.Name)) entry.ExtractToFile(targetFile, overwrite: true);
                }
        }

        static long ReadInt64(byte[] buf, int offset)
        {
            long value = buf[offset + 7] & 0x7F;
            for (int index = 6; index >= 0; index--) { value *= 256; value += buf[offset + index]; }
            if ((buf[offset + 7] & 0x80) != 0) value = -value;
            return value;
        }

        [Fact(Skip = "Rewrite this test, the original uses too many heavyweight fixtures")]
        public void ApplyMultipleDeltaPackagesGeneratesCorrectHash()
        {
            Assert.True(false, "Rewrite this test, the original uses too many heavyweight fixtures");
        }
    }

    public class CreateDeltaPackageTests : IEnableLogger
    {
        [Fact]
        public void CreateDeltaPackageIntegrationTest()
        {
            var basePackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Tests.0.1.0-pre.nupkg");
            var newPackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Tests.0.2.0-pre.nupkg");

            var sourceDir = IntegrationTestHelper.GetPath("fixtures", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            var baseFixture = new ReleasePackage(basePackage);
            var fixture = new ReleasePackage(newPackage);

            var tempFiles = Enumerable.Range(0, 3)
                .Select(_ => Path.GetTempPath() + Guid.NewGuid().ToString() + ".nupkg")
                .ToArray();

            try {
                baseFixture.CreateReleasePackage(tempFiles[0], sourceDir);
                fixture.CreateReleasePackage(tempFiles[1], sourceDir);

                (new FileInfo(baseFixture.ReleasePackageFile)).Exists.ShouldBeTrue();
                (new FileInfo(fixture.ReleasePackageFile)).Exists.ShouldBeTrue();

                var deltaBuilder = new DeltaPackageBuilder();
                deltaBuilder.CreateDeltaPackage(baseFixture, fixture, tempFiles[2]);

                var fullPkg = new ZipPackage(tempFiles[1]);
                var deltaPkg = new ZipPackage(tempFiles[2]);

                //
                // Package Checks
                //

                fullPkg.Id.ShouldEqual(deltaPkg.Id);
                fullPkg.Version.CompareTo(deltaPkg.Version).ShouldEqual(0);

                // Delta packages should be smaller than the original!
                var fileInfos = tempFiles.Select(x => new FileInfo(x)).ToArray();
                this.Log().Info("Base Size: {0}, Current Size: {1}, Delta Size: {2}",
                    fileInfos[0].Length, fileInfos[1].Length, fileInfos[2].Length);

                (fileInfos[2].Length - fileInfos[1].Length).ShouldBeLessThan(0);

                //
                // File Checks
                ///

                var deltaPkgFiles = deltaPkg.GetFiles().ToList();
                deltaPkgFiles.Count.ShouldBeGreaterThan(0);

                this.Log().Info("Files in delta package:");
                deltaPkgFiles.ForEach(x => this.Log().Info(x.Path));

                var newFilesAdded = new[] {
                    "Newtonsoft.Json.dll",
                    "Refit.dll",
                    "Refit-Portable.dll",
                    "Castle.Core.dll",
                }.Select(x => x.ToLowerInvariant());

                // vNext adds a dependency on Refit
                newFilesAdded
                    .All(x => deltaPkgFiles.Any(y => y.Path.ToLowerInvariant().Contains(x)))
                    .ShouldBeTrue();

                // All the other files should be diffs and shasums
                deltaPkgFiles
                    .Where(x => !newFilesAdded.Any(y => x.Path.ToLowerInvariant().Contains(y)))
                    .All(x => x.Path.ToLowerInvariant().EndsWith("diff") || x.Path.ToLowerInvariant().EndsWith("shasum"))
                    .ShouldBeTrue();

                // Every .diff file should have a shasum file
                deltaPkg.GetFiles().Any(x => x.Path.ToLowerInvariant().EndsWith(".diff")).ShouldBeTrue();
                deltaPkg.GetFiles()
                    .Where(x => x.Path.ToLowerInvariant().EndsWith(".diff"))
                    .ForEach(x => {
                        var lookingFor = x.Path.Replace(".diff", ".shasum");
                        this.Log().Info("Looking for corresponding shasum file: {0}", lookingFor);
                        deltaPkg.GetFiles().Any(y => y.Path == lookingFor).ShouldBeTrue();
                    });
            } finally {
                tempFiles.ForEach(File.Delete);
            }
        }

        [Fact]
        public void WhenBasePackageIsNewerThanNewPackageThrowException()
        {
            var basePackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Tests.0.2.0-pre.nupkg");
            var newPackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Tests.0.1.0-pre.nupkg");

            var sourceDir = IntegrationTestHelper.GetPath("fixtures", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            var baseFixture = new ReleasePackage(basePackage);
            var fixture = new ReleasePackage(newPackage);

            var tempFiles = Enumerable.Range(0, 3)
                .Select(_ => Path.GetTempPath() + Guid.NewGuid().ToString() + ".nupkg")
                .ToArray();

            try {
                baseFixture.CreateReleasePackage(tempFiles[0], sourceDir);
                fixture.CreateReleasePackage(tempFiles[1], sourceDir);

                (new FileInfo(baseFixture.ReleasePackageFile)).Exists.ShouldBeTrue();
                (new FileInfo(fixture.ReleasePackageFile)).Exists.ShouldBeTrue();

                Assert.Throws<InvalidOperationException>(() =>
                {
                    var deltaBuilder = new DeltaPackageBuilder();
                    deltaBuilder.CreateDeltaPackage(baseFixture, fixture, tempFiles[2]);
                });
            } finally {
                tempFiles.ForEach(File.Delete);
            }
        }

        [Fact]
        public void WhenBasePackageReleaseIsNullThrowsException()
        {
            var basePackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.0.0.0.nupkg");
            var newPackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.1.0.0.nupkg");

            var sourceDir = IntegrationTestHelper.GetPath("fixtures", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            var baseFixture = new ReleasePackage(basePackage);
            var fixture = new ReleasePackage(newPackage);

            var tempFile = Path.GetTempPath() + Guid.NewGuid() + ".nupkg";

            try {
                Assert.Throws<ArgumentException>(() => {
                    var deltaBuilder = new DeltaPackageBuilder();
                    deltaBuilder.CreateDeltaPackage(baseFixture, fixture, tempFile);
                });
            } finally {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void WhenBasePackageDoesNotExistThrowException()
        {
            var basePackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Tests.0.1.0-pre.nupkg");
            var newPackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Tests.0.2.0-pre.nupkg");

            var sourceDir = IntegrationTestHelper.GetPath("fixtures", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            var baseFixture = new ReleasePackage(basePackage);
            var fixture = new ReleasePackage(newPackage);

            var tempFiles = Enumerable.Range(0, 3)
                .Select(_ => Path.GetTempPath() + Guid.NewGuid().ToString() + ".nupkg")
                .ToArray();

            try {
                baseFixture.CreateReleasePackage(tempFiles[0], sourceDir);
                fixture.CreateReleasePackage(tempFiles[1], sourceDir);

                (new FileInfo(baseFixture.ReleasePackageFile)).Exists.ShouldBeTrue();
                (new FileInfo(fixture.ReleasePackageFile)).Exists.ShouldBeTrue();

                // NOW WATCH AS THE FILE DISAPPEARS
                File.Delete(baseFixture.ReleasePackageFile);

                Assert.Throws<FileNotFoundException>(() => {
                    var deltaBuilder = new DeltaPackageBuilder();
                    deltaBuilder.CreateDeltaPackage(baseFixture, fixture, tempFiles[2]);
                });
            } finally {
                tempFiles.ForEach(File.Delete);
            }
        }

        [Fact]
        public void WhenNewPackageDoesNotExistThrowException()
        {
            var basePackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Tests.0.1.0-pre.nupkg");
            var newPackage = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Tests.0.2.0-pre.nupkg");

            var sourceDir = IntegrationTestHelper.GetPath("fixtures", "packages");
            (new DirectoryInfo(sourceDir)).Exists.ShouldBeTrue();

            var baseFixture = new ReleasePackage(basePackage);
            var fixture = new ReleasePackage(newPackage);

            var tempFiles = Enumerable.Range(0, 3)
                .Select(_ => Path.GetTempPath() + Guid.NewGuid().ToString() + ".nupkg")
                .ToArray();

            try {
                baseFixture.CreateReleasePackage(tempFiles[0], sourceDir);
                fixture.CreateReleasePackage(tempFiles[1], sourceDir);

                (new FileInfo(baseFixture.ReleasePackageFile)).Exists.ShouldBeTrue();
                (new FileInfo(fixture.ReleasePackageFile)).Exists.ShouldBeTrue();

                // NOW WATCH AS THE FILE DISAPPEARS
                File.Delete(fixture.ReleasePackageFile);

                Assert.Throws<FileNotFoundException>(() => {
                    var deltaBuilder = new DeltaPackageBuilder();
                    deltaBuilder.CreateDeltaPackage(baseFixture, fixture, tempFiles[2]);
                });
            } finally {
                tempFiles.ForEach(File.Delete);
            }
        }

        [Fact]
        public void HandleBsDiffWithoutExtraData()
        {
            var baseFileData = new byte[] { 1, 1, 1, 1 };
            var newFileData = new byte[] { 2, 1, 1, 1 };

            byte[] patchData;

            using (var patchOut = new MemoryStream())
            {
                Bsdiff.BinaryPatchUtility.Create(baseFileData, newFileData, patchOut);
                patchData = patchOut.ToArray();
            }

            using (var toPatch = new MemoryStream(baseFileData))
            using (var patched = new MemoryStream())
            {
                Bsdiff.BinaryPatchUtility.Apply(toPatch, () => new MemoryStream(patchData), patched);

                Assert.Equal(newFileData, patched.ToArray());
            }
        }
    }
}
