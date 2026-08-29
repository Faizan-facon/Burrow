using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Squirrel.SimpleSplat;
using Squirrel.Tests.TestHelpers;
using Xunit;

namespace Squirrel.Tests
{
    public class DownloadReleasesTests : IEnableLogger
    {
        [Fact]
        public void ChecksumShouldFailIfFilesAreMissing()
        {
            string tempDir;
            using (Utility.WithTempDirectory(out tempDir)) {
                var pkgDir = Path.Combine(tempDir, "packages");
                Directory.CreateDirectory(pkgDir);

                var nuGetPkg = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.0.0.0-full.nupkg");
                var entry = ReleaseEntry.GenerateFromFile(nuGetPkg);

                var impl = new UpdateManager.DownloadReleasesImpl(tempDir);
                var ex = Assert.Throws<Exception>(() => {
                    var method = typeof(UpdateManager.DownloadReleasesImpl).GetMethod("checksumPackage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    try {
                        method.Invoke(impl, new object[] { entry });
                    } catch (System.Reflection.TargetInvocationException tie) {
                        throw tie.InnerException;
                    }
                });

                Assert.Contains("doesn't exist", ex.Message);
            }
        }

        [Fact]
        public void ChecksumShouldFailIfFilesAreBogus()
        {
            string tempDir;
            using (Utility.WithTempDirectory(out tempDir)) {
                var pkgDir = Path.Combine(tempDir, "packages");
                Directory.CreateDirectory(pkgDir);

                var nuGetPkg = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.0.0.0-full.nupkg");
                var entry = ReleaseEntry.GenerateFromFile(nuGetPkg);

                var targetFile = Path.Combine(pkgDir, entry.Filename);
                File.WriteAllText(targetFile, "corrupted bogus content");

                var impl = new UpdateManager.DownloadReleasesImpl(tempDir);
                var ex = Assert.Throws<Exception>(() => {
                    var method = typeof(UpdateManager.DownloadReleasesImpl).GetMethod("checksumPackage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    try {
                        method.Invoke(impl, new object[] { entry });
                    } catch (System.Reflection.TargetInvocationException tie) {
                        throw tie.InnerException;
                    }
                });

                Assert.True(ex.Message.Contains("size doesn't match") || ex.Message.Contains("Checksum doesn't match"));
                Assert.False(File.Exists(targetFile));
            }
        }

        [Fact]
        public async Task DownloadReleasesFromHttpServerIntegrationTest()
        {
            string sourceDir;
            string targetDir;
            using (Utility.WithTempDirectory(out sourceDir))
            using (Utility.WithTempDirectory(out targetDir)) {
                var fixturePkg = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.0.0.0-full.nupkg");
                var sourceFile = Path.Combine(sourceDir, Path.GetFileName(fixturePkg));
                File.Copy(fixturePkg, sourceFile);

                var entry = ReleaseEntry.GenerateFromFile(sourceFile);
                var entries = new[] { entry };

                ReleaseEntry.WriteReleaseFile(entries, Path.Combine(sourceDir, "RELEASES"));

                int port = new Random().Next(32000, 48000);
                using (var listener = new HttpListener()) {
                    var prefix = $"http://127.0.0.1:{port}/";
                    listener.Prefixes.Add(prefix);
                    try {
                        listener.Start();
                    } catch (HttpListenerException) {
                        port = new Random().Next(48001, 60000);
                        prefix = $"http://127.0.0.1:{port}/";
                        listener.Prefixes.Clear();
                        listener.Prefixes.Add(prefix);
                        listener.Start();
                    }

                    var serverTask = Task.Run(async () => {
                        while (listener.IsListening) {
                            try {
                                var ctx = await listener.GetContextAsync();
                                var requestedFile = ctx.Request.Url.AbsolutePath.TrimStart('/');
                                var localFile = Path.Combine(sourceDir, requestedFile);
                                if (File.Exists(localFile)) {
                                    ctx.Response.StatusCode = 200;
                                    ctx.Response.ContentType = "application/octet-stream";
                                    using (var fs = File.OpenRead(localFile)) {
                                        ctx.Response.ContentLength64 = fs.Length;
                                        await fs.CopyToAsync(ctx.Response.OutputStream);
                                    }
                                } else {
                                    ctx.Response.StatusCode = 404;
                                }
                                ctx.Response.Close();
                            } catch {
                                break;
                            }
                        }
                    });

                    var packagesDir = Path.Combine(targetDir, "theApp", "packages");
                    Directory.CreateDirectory(packagesDir);

                    using (var mgr = new UpdateManager(prefix, "theApp", targetDir)) {
                        var progressList = new List<int>();
                        await mgr.DownloadReleases(entries, progressList.Add);

                        Assert.True(progressList.Count > 0);
                        Assert.Equal(100, progressList.Last());
                    }

                    listener.Stop();
                    await Task.WhenAny(serverTask, Task.Delay(500));

                    var downloadedFile = Path.Combine(targetDir, "theApp", "packages", entry.Filename);
                    Assert.True(File.Exists(downloadedFile));
                    var actualEntry = ReleaseEntry.GenerateFromFile(downloadedFile);
                    Assert.Equal(entry.SHA1, actualEntry.SHA1);
                    Assert.Equal(entry.Filesize, actualEntry.Filesize);
                }
            }
        }

        [Fact]
        public async Task DownloadReleasesFromFileDirectoryIntegrationTest()
        {
            string sourceDir;
            string targetDir;
            using (Utility.WithTempDirectory(out sourceDir))
            using (Utility.WithTempDirectory(out targetDir)) {
                var fixturePkg = IntegrationTestHelper.GetPath("fixtures", "Squirrel.Core.1.0.0.0-full.nupkg");
                var sourceFile = Path.Combine(sourceDir, Path.GetFileName(fixturePkg));
                File.Copy(fixturePkg, sourceFile);

                var entry = ReleaseEntry.GenerateFromFile(sourceFile);
                var entries = new[] { entry };

                ReleaseEntry.WriteReleaseFile(entries, Path.Combine(sourceDir, "RELEASES"));

                var packagesDir = Path.Combine(targetDir, "theApp", "packages");
                Directory.CreateDirectory(packagesDir);

                using (var mgr = new UpdateManager(sourceDir, "theApp", targetDir)) {
                    var progressList = new List<int>();

                    await mgr.DownloadReleases(entries, progressList.Add);

                    Assert.True(progressList.Count > 0);
                    Assert.Equal(100, progressList.Last());
                }

                var downloadedFile = Path.Combine(targetDir, "theApp", "packages", entry.Filename);
                Assert.True(File.Exists(downloadedFile));

                var actualEntry = ReleaseEntry.GenerateFromFile(downloadedFile);
                Assert.Equal(entry.SHA1, actualEntry.SHA1);
                Assert.Equal(entry.Filesize, actualEntry.Filesize);
            }
        }
    }
}
