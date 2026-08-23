using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Squirrel.SimpleSplat;
using System.ComponentModel;
using Squirrel.Bsdiff;
using System.IO.Compression;

namespace Squirrel
{
    public interface IDeltaPackageBuilder
    {
        ReleasePackage CreateDeltaPackage(ReleasePackage basePackage, ReleasePackage newPackage, string outputFile);
        ReleasePackage ApplyDeltaPackage(ReleasePackage basePackage, ReleasePackage deltaPackage, string outputFile);
    }

    public class DeltaPackageBuilder : IEnableLogger, IDeltaPackageBuilder
    {
        readonly string localAppDirectory;
        public DeltaPackageBuilder(string localAppDataOverride = null)
        {
            this.localAppDirectory = localAppDataOverride;
        }

        public ReleasePackage CreateDeltaPackage(ReleasePackage basePackage, ReleasePackage newPackage, string outputFile)
        {
            Contract.Requires(basePackage != null);
            Contract.Requires(!String.IsNullOrEmpty(outputFile) && !File.Exists(outputFile));

            if (basePackage.Version > newPackage.Version) {
                var message = String.Format(
                    "You cannot create a delta package based on version {0} as it is a later version than {1}",
                    basePackage.Version,
                    newPackage.Version);
                throw new InvalidOperationException(message);
            }

            if (basePackage.ReleasePackageFile == null) {
                throw new ArgumentException("The base package's release file is null", "basePackage");
            }

            if (!File.Exists(basePackage.ReleasePackageFile)) {
                throw new FileNotFoundException("The base package release does not exist", basePackage.ReleasePackageFile);
            }

            if (!File.Exists(newPackage.ReleasePackageFile)) {
                throw new FileNotFoundException("The new package release does not exist", newPackage.ReleasePackageFile);
            }

            string baseTempPath = null;
            string tempPath = null;

            using (Utility.WithTempDirectory(out baseTempPath, null))
            using (Utility.WithTempDirectory(out tempPath, null)) {
                var baseTempInfo = new DirectoryInfo(baseTempPath);
                var tempInfo = new DirectoryInfo(tempPath);

                this.Log().Info("Extracting {0} and {1} into {2}", 
                    basePackage.ReleasePackageFile, newPackage.ReleasePackageFile, tempPath);

                Utility.ExtractZipToDirectory(basePackage.ReleasePackageFile, baseTempInfo.FullName).Wait();
                Utility.ExtractZipToDirectory(newPackage.ReleasePackageFile, tempInfo.FullName).Wait();

                // Collect a list of relative paths under 'lib' and map them
                // to their full name. We'll use this later to determine in
                // the new version of the package whether the file exists or
                // not.
                var baseLibFiles = baseTempInfo.GetAllFilesRecursively()
                    .Where(x => x.FullName.ToLowerInvariant().Contains("lib" + Path.DirectorySeparatorChar))
                    .ToDictionary(k => getRelativePath(k.FullName, baseTempInfo.FullName).Replace('/', '\\').ToLowerInvariant(), v => v.FullName, StringComparer.OrdinalIgnoreCase);

                var newLibDir = tempInfo.GetDirectories().First(x => x.Name.ToLowerInvariant() == "lib");

                var libFiles = newLibDir.GetAllFilesRecursively().ToList();
                Parallel.ForEach(libFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, libFile => {
                    createDeltaForSingleFile(libFile, tempInfo, baseLibFiles);
                });

                ReleasePackage.addDeltaFilesToContentTypes(tempInfo.FullName);
                Utility.CreateZipFromDirectory(outputFile, tempInfo.FullName).Wait();
            }

            return new ReleasePackage(outputFile);
        }

        public ReleasePackage ApplyDeltaPackage(ReleasePackage basePackage, ReleasePackage deltaPackage, string outputFile)
        {
            return ApplyDeltaPackage(basePackage, deltaPackage, outputFile, x => { });
        }

        public ReleasePackage ApplyDeltaPackage(ReleasePackage basePackage, ReleasePackage deltaPackage, string outputFile, Action<int> progress)
        {
            Contract.Requires(deltaPackage != null);
            Contract.Requires(!String.IsNullOrEmpty(outputFile) && !File.Exists(outputFile));

            string workingPath;
            string deltaPath;

            using (Utility.WithTempDirectory(out deltaPath, localAppDirectory))
            using (Utility.WithTempDirectory(out workingPath, localAppDirectory)) {
                using (var za = ZipFile.OpenRead(deltaPackage.InputPackageFile)) {
                    foreach (var entry in za.Entries) {
                        var targetFile = Path.Combine(deltaPath, entry.FullName);
                        var dir = Path.GetDirectoryName(targetFile);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        if (!string.IsNullOrEmpty(entry.Name)) {
                            entry.ExtractToFile(targetFile, overwrite: true);
                        }
                    }
                }

                progress(25);

                using (var za = ZipFile.OpenRead(basePackage.InputPackageFile)) {
                    foreach (var entry in za.Entries) {
                        var targetFile = Path.Combine(workingPath, entry.FullName);
                        var dir = Path.GetDirectoryName(targetFile);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        if (!string.IsNullOrEmpty(entry.Name)) {
                            entry.ExtractToFile(targetFile, overwrite: true);
                        }
                    }
                }

                progress(50);

                var deltaDir = new DirectoryInfo(deltaPath);
                var deltaPathRelativePaths = deltaDir.GetAllFilesRecursively()
                    .Select(x => getRelativePath(x.FullName, deltaPath))
                    .ToArray();

                var diffFiles = deltaPathRelativePaths
                    .Where(x => x.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase))
                    .Where(x => !x.EndsWith(".shasum", StringComparison.InvariantCultureIgnoreCase))
                    .Where(x => !x.EndsWith(".diff", StringComparison.InvariantCultureIgnoreCase) ||
                                !deltaPathRelativePaths.Contains(x.Substring(0, x.Length - 5) + ".bsdiff", StringComparer.OrdinalIgnoreCase))
                    .ToList();

                var pathsVisited = new System.Collections.Concurrent.ConcurrentBag<string>();

                // Apply all of the .diff files in parallel
                Parallel.ForEach(diffFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file => {
                    var cleanName = Regex.Replace(file, @"\.(bs)?diff$", "", RegexOptions.IgnoreCase).Replace('/', '\\').ToLowerInvariant();
                    pathsVisited.Add(cleanName);
                    applyDiffToFile(deltaPath, file, workingPath);
                });

                progress(75);

                var visitedSet = new HashSet<string>(pathsVisited, StringComparer.OrdinalIgnoreCase);

                // Delete all of the files that were in the old package but
                // not in the new one.
                new DirectoryInfo(workingPath).GetAllFilesRecursively()
                    .Select(x => getRelativePath(x.FullName, workingPath).Replace('/', '\\').ToLowerInvariant())
                    .Where(x => x.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase) && !visitedSet.Contains(x))
                    .ForEach(x => {
                        this.Log().Info("{0} was in old package but not in new one, deleting", x);
                        File.Delete(Path.Combine(workingPath, x));
                    });

                progress(80);

                // Update all the files that aren't in 'lib' with the delta
                // package's versions (i.e. the nuspec file, etc etc).
                deltaPathRelativePaths
                    .Where(x => !x.StartsWith("lib", StringComparison.InvariantCultureIgnoreCase))
                    .ForEach(x => {
                        this.Log().Info("Updating metadata file: {0}", x);
                        var targetDest = Path.Combine(workingPath, x);
                        var targetDestDir = Path.GetDirectoryName(targetDest);
                        if (!Directory.Exists(targetDestDir)) Directory.CreateDirectory(targetDestDir);
                        File.Copy(Path.Combine(deltaPath, x), targetDest, true);
                    });

                ReleasePackage.removeDeltaFilesFromContentTypes(workingPath);

                this.Log().Info("Repacking into full package: {0}", outputFile);
                Utility.CreateZipFromDirectory(outputFile, workingPath).Wait();

                progress(100);
            }

            return new ReleasePackage(outputFile);
        }

        void createDeltaForSingleFile(FileInfo targetFile, DirectoryInfo workingDirectory, Dictionary<string, string> baseFileListing)
        {
            // NB: There are three cases here that we'll handle:
            //
            // 1. Exists only in new => leave it alone, we'll use it directly.
            // 2. Exists in both old and new => write a dummy file so we know
            //    to keep it.
            // 3. Exists in old but changed in new => create a delta file
            //
            // The fourth case of "Exists only in old => delete it in new"
            // is handled when we apply the delta package
            var relativePath = getRelativePath(targetFile.FullName, workingDirectory.FullName).Replace('/', '\\').ToLowerInvariant();

            if (!baseFileListing.ContainsKey(relativePath)) {
                this.Log().Info("{0} not found in base package, marking as new", relativePath);
                return;
            }

            var oldData = File.ReadAllBytes(baseFileListing[relativePath]);
            var newData = File.ReadAllBytes(targetFile.FullName);

            if (bytesAreIdentical(oldData, newData)) {
                this.Log().Info("{0} hasn't changed, writing dummy file", relativePath);

                File.Create(targetFile.FullName + ".diff").Dispose();
                File.Create(targetFile.FullName + ".shasum").Dispose();
                targetFile.Delete();
                return;
            }

            this.Log().Info("Delta patching {0} => {1}", baseFileListing[relativePath], targetFile.FullName);

            try {
                using (FileStream of = File.Create(targetFile.FullName + ".bsdiff")) {
                    var stats = BinaryPatchUtility.Create(oldData, newData, of);
                    this.Log().Info("Delta created for {0}: {1}", targetFile.Name, stats);

                    // NB: Create a dummy corrupt .diff file so that older 
                    // versions which don't understand bsdiff will fail out
                    // until they get upgraded, instead of seeing the missing
                    // file and just removing it.
                    File.WriteAllText(targetFile.FullName + ".diff", "1");
                }
            } catch (Exception ex) {
                this.Log().WarnException(String.Format("We really couldn't create a delta for {0}", targetFile.Name), ex);

                Utility.DeleteFileHarder(targetFile.FullName + ".bsdiff", true);
                Utility.DeleteFileHarder(targetFile.FullName + ".diff", true);
                return;
            }

            var rl = ReleaseEntry.GenerateFromFile(new MemoryStream(newData), targetFile.Name + ".shasum");
            File.WriteAllText(targetFile.FullName + ".shasum", rl.EntryAsString, Encoding.UTF8);
            targetFile.Delete();
        }


        void applyDiffToFile(string deltaPath, string relativeFilePath, string workingDirectory)
        {
            var inputFile = Path.Combine(deltaPath, relativeFilePath);
            var finalTarget = Path.Combine(workingDirectory, Regex.Replace(relativeFilePath, @"\.(bs)?diff$", ""));

            var tempTargetFile = Path.Combine(Path.GetTempPath(), "burrow_patch_" + Guid.NewGuid().ToString("N") + ".tmp");

            try {
                // NB: Zero-length diffs indicate the file hasn't actually changed
                if (new FileInfo(inputFile).Length == 0) {
                    this.Log().Info("{0} exists unchanged, skipping", relativeFilePath);
                    return;
                }

                 if (relativeFilePath.EndsWith(".bsdiff", StringComparison.InvariantCultureIgnoreCase)) {
                    using (var of = File.OpenWrite(tempTargetFile))
                    using (var inf = File.OpenRead(finalTarget)) {
                        this.Log().Info("Applying BSDiff to {0}", relativeFilePath);
                        BinaryPatchUtility.Apply(inf, () => File.OpenRead(inputFile), of);
                    }

                    verifyPatchedFile(relativeFilePath, inputFile, tempTargetFile);
                 } else if (relativeFilePath.EndsWith(".diff", StringComparison.InvariantCultureIgnoreCase)) {
                    this.Log().Info("Applying MSDiff to {0}", relativeFilePath);
                    var msDelta = new MsDeltaCompression();
                    msDelta.ApplyDelta(inputFile, finalTarget, tempTargetFile);

                    verifyPatchedFile(relativeFilePath, inputFile, tempTargetFile);
                } else {
                    using (var of = File.OpenWrite(tempTargetFile))
                    using (var inf = File.OpenRead(inputFile)) {
                        this.Log().Info("Adding new file: {0}", relativeFilePath);
                        inf.CopyTo(of);
                    }
                }

                if (File.Exists(finalTarget)) File.Delete(finalTarget);

                var targetPath = Directory.GetParent(finalTarget);
                if (!targetPath.Exists) targetPath.Create();

                File.Move(tempTargetFile, finalTarget);
            } finally {
                if (File.Exists(tempTargetFile)) Utility.DeleteFileHarder(tempTargetFile, true);
            }
        }

        void verifyPatchedFile(string relativeFilePath, string inputFile, string tempTargetFile)
        {
            var shaFile = Regex.Replace(inputFile, @"\.(bs)?diff$", ".shasum");
            var expectedReleaseEntry = ReleaseEntry.ParseReleaseEntry(File.ReadAllText(shaFile, Encoding.UTF8));
            var actualReleaseEntry = ReleaseEntry.GenerateFromFile(tempTargetFile);

            if (expectedReleaseEntry.Filesize != actualReleaseEntry.Filesize) {
                this.Log().Warn("Patched file {0} has incorrect size, expected {1}, got {2}", relativeFilePath,
                    expectedReleaseEntry.Filesize, actualReleaseEntry.Filesize);
                throw new ChecksumFailedException() {Filename = relativeFilePath};
            }

            if (expectedReleaseEntry.SHA1 != actualReleaseEntry.SHA1) {
                this.Log().Warn("Patched file {0} has incorrect SHA1, expected {1}, got {2}", relativeFilePath,
                    expectedReleaseEntry.SHA1, actualReleaseEntry.SHA1);
                throw new ChecksumFailedException() {Filename = relativeFilePath};
            }
        }

        unsafe bool bytesAreIdentical(byte[] oldData, byte[] newData)
        {
            if (oldData == null || newData == null) {
                return oldData == newData;
            }
            if (oldData.Length != newData.Length) {
                return false;
            }

            int len = oldData.Length;
            fixed (byte* pOld = oldData, pNew = newData)
            {
                ulong* ptrOld = (ulong*)pOld;
                ulong* ptrNew = (ulong*)pNew;
                while (len >= 8)
                {
                    if (*ptrOld != *ptrNew) return false;
                    ptrOld++;
                    ptrNew++;
                    len -= 8;
                }

                byte* pbOld = (byte*)ptrOld;
                byte* pbNew = (byte*)ptrNew;
                while (len > 0)
                {
                    if (*pbOld != *pbNew) return false;
                    pbOld++;
                    pbNew++;
                    len--;
                }
            }
            return true;
        }

        static string getRelativePath(string fullPath, string basePath)
        {
            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }
    }
}
