using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NuGet
{
    public interface IPackageFile
    {
        string Path { get; }
        string EffectivePath { get; }
        FrameworkName TargetFramework { get; }
        Stream GetStream();
    }

    public interface IPackage
    {
        string Id { get; }
        SemanticVersion Version { get; }
        string Title { get; }
        string Description { get; }
        string Summary { get; }
        string ReleaseNotes { get; }
        string Copyright { get; }
        string Language { get; }
        IEnumerable<string> Authors { get; }
        Uri IconUrl { get; }
        Uri ProjectUrl { get; }
        IEnumerable<PackageDependencySet> DependencySets { get; }
        IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get; }
        IEnumerable<FrameworkName> GetSupportedFrameworks();
        IEnumerable<IPackageFile> GetFiles();
        IEnumerable<IPackageFile> GetLibFiles();
        IEnumerable<IPackageFile> GetContentFiles();
        string GetFullName();
        Stream GetStream();
    }

    public class FrameworkAssemblyReference
    {
        public FrameworkAssemblyReference(string assemblyName, IEnumerable<FrameworkName> supportedFrameworks = null)
        {
            AssemblyName = assemblyName;
            SupportedFrameworks = supportedFrameworks ?? Enumerable.Empty<FrameworkName>();
        }

        public string AssemblyName { get; }
        public IEnumerable<FrameworkName> SupportedFrameworks { get; }
    }

    public class ZipPackageFile : IPackageFile
    {
        private readonly Func<Stream> streamFactory;
        private readonly byte[] data;

        public ZipPackageFile(string path, Func<Stream> streamFactory, FrameworkName targetFramework = null)
        {
            Path = path.Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar);
            this.streamFactory = streamFactory;
            TargetFramework = targetFramework ?? ParseTargetFrameworkFromPath(Path);
        }

        public ZipPackageFile(string path, byte[] data, FrameworkName targetFramework = null)
        {
            Path = path.Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar);
            this.data = data;
            TargetFramework = targetFramework ?? ParseTargetFrameworkFromPath(Path);
        }

        public string Path { get; }

        public string EffectivePath
        {
            get
            {
                var parts = Path.Split(new[] { '/', '\\' }, 2);
                if (parts.Length > 1 && string.Equals(parts[0], "content", StringComparison.OrdinalIgnoreCase))
                {
                    return parts[1];
                }
                if (parts.Length > 1 && string.Equals(parts[0], "lib", StringComparison.OrdinalIgnoreCase))
                {
                    var libParts = parts[1].Split(new[] { '/', '\\' }, 2);
                    return libParts.Length > 1 ? libParts[1] : parts[1];
                }
                return Path;
            }
        }

        public FrameworkName TargetFramework { get; }

        public Stream GetStream()
        {
            if (streamFactory != null)
                return streamFactory();
            if (data != null)
                return new MemoryStream(data, writable: false);
            throw new InvalidOperationException("Package file stream is not available.");
        }

        public static FrameworkName ParseTargetFrameworkFromPath(string path)
        {
            var parts = path.Split('/', '\\');
            if (parts.Length > 1 && string.Equals(parts[0], "lib", StringComparison.OrdinalIgnoreCase))
            {
                return ParseFrameworkName(parts[1]);
            }
            return null;
        }

        public static FrameworkName ParseFrameworkName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = name.Trim();
            if (name.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(".NETStandard", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(".NETCoreApp", StringComparison.OrdinalIgnoreCase))
            {
                try { return new FrameworkName(name); } catch { }
            }

            var lower = name.ToLowerInvariant();
            if (lower.StartsWith("net48") || lower.StartsWith(".netframework4.8")) return new FrameworkName(".NETFramework,Version=v4.8");
            if (lower.StartsWith("net472") || lower.StartsWith(".netframework4.7.2")) return new FrameworkName(".NETFramework,Version=v4.7.2");
            if (lower.StartsWith("net471") || lower.StartsWith(".netframework4.7.1")) return new FrameworkName(".NETFramework,Version=v4.7.1");
            if (lower.StartsWith("net47") || lower.StartsWith(".netframework4.7")) return new FrameworkName(".NETFramework,Version=v4.7");
            if (lower.StartsWith("net462") || lower.StartsWith(".netframework4.6.2")) return new FrameworkName(".NETFramework,Version=v4.6.2");
            if (lower.StartsWith("net461") || lower.StartsWith(".netframework4.6.1")) return new FrameworkName(".NETFramework,Version=v4.6.1");
            if (lower.StartsWith("net46") || lower.StartsWith(".netframework4.6")) return new FrameworkName(".NETFramework,Version=v4.6");
            if (lower.StartsWith("net452") || lower.StartsWith(".netframework4.5.2")) return new FrameworkName(".NETFramework,Version=v4.5.2");
            if (lower.StartsWith("net451") || lower.StartsWith(".netframework4.5.1")) return new FrameworkName(".NETFramework,Version=v4.5.1");
            if (lower.StartsWith("net45") || lower.StartsWith(".netframework4.5")) return new FrameworkName(".NETFramework,Version=v4.5");
            if (lower.StartsWith("net40-client") || lower.StartsWith("net40_client") || lower.StartsWith(".netframework4.0-client")) return new FrameworkName(".NETFramework", new Version(4, 0), "Client");
            if (lower.StartsWith("net35-client") || lower.StartsWith("net35_client") || lower.StartsWith(".netframework3.5-client")) return new FrameworkName(".NETFramework", new Version(3, 5), "Client");
            if (lower.StartsWith("net40") || lower.StartsWith("net4.0") || lower.StartsWith(".netframework4.0")) return new FrameworkName(".NETFramework,Version=v4.0");
            if (lower.StartsWith("net35") || lower.StartsWith("net3.5") || lower.StartsWith(".netframework3.5")) return new FrameworkName(".NETFramework,Version=v3.5");
            if (lower.StartsWith("net20") || lower.StartsWith("net2.0") || lower.StartsWith(".netframework2.0")) return new FrameworkName(".NETFramework,Version=v2.0");

            if (lower.StartsWith("netstandard2.1")) return new FrameworkName(".NETStandard,Version=v2.1");
            if (lower.StartsWith("netstandard2.0")) return new FrameworkName(".NETStandard,Version=v2.0");
            if (lower.StartsWith("netstandard")) return new FrameworkName(".NETStandard,Version=v1.0");

            if (lower.StartsWith("sl") || lower.StartsWith("silverlight")) return new FrameworkName("Silverlight,Version=v4.0");
            if (lower.StartsWith("wp") || lower.StartsWith("windowsphone")) return new FrameworkName("WindowsPhone,Version=v8.0");
            if (lower.StartsWith("win8") || lower.StartsWith("windows8") || lower.StartsWith("winrt")) return new FrameworkName("Windows,Version=v8.0");
            if (lower.StartsWith("portable-") || lower.StartsWith(".netportable-")) return new FrameworkName(".NETPortable", new Version(0, 0), lower.StartsWith("portable-") ? name.Substring(9) : name.Substring(13));
            if (lower.StartsWith("portable") || lower.StartsWith(".netportable")) return new FrameworkName(".NETPortable,Version=v0.0");

            try
            {
                return new FrameworkName(name);
            }
            catch
            {
                return new FrameworkName($"Unknown,Profile={name},Version=v0.0");
            }
        }
    }

    public class ZipPackage : IPackage
    {
        private readonly List<IPackageFile> files = new List<IPackageFile>();
        private readonly List<PackageDependencySet> dependencySets = new List<PackageDependencySet>();
        private readonly string packageFilePath;
        private byte[] rawPackageBytes;

        public ZipPackage(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Package file not found.", filePath);

            packageFilePath = filePath;
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                ReadPackage(stream, filePath);
            }
        }

        public ZipPackage(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            ReadPackage(stream, null);
        }

        public string Id { get; private set; }
        public SemanticVersion Version { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string Summary { get; private set; }
        public string ReleaseNotes { get; private set; }
        public string Copyright { get; private set; }
        public string Language { get; private set; }
        public IEnumerable<string> Authors { get; private set; } = Enumerable.Empty<string>();
        public Uri IconUrl { get; private set; }
        public Uri ProjectUrl { get; private set; }
        public IEnumerable<PackageDependencySet> DependencySets => dependencySets;
        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies => frameworkAssemblies;
        private readonly List<FrameworkAssemblyReference> frameworkAssemblies = new List<FrameworkAssemblyReference>();

        public string GetFullName() => $"{Id}.{Version}";

        public IEnumerable<FrameworkName> GetSupportedFrameworks()
        {
            return GetLibFiles()
                .Select(f => f.TargetFramework)
                .Where(fn => fn != null)
                .Distinct();
        }

        public IEnumerable<IPackageFile> GetFiles() => files;

        public IEnumerable<IPackageFile> GetLibFiles()
        {
            return files.Where(f => (f.Path.StartsWith("lib" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                                     f.Path.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) ||
                                     f.Path.StartsWith("lib\\", StringComparison.OrdinalIgnoreCase)) &&
                                    (f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                                     f.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                                     f.Path.EndsWith(".winmd", StringComparison.OrdinalIgnoreCase)));
        }

        public IEnumerable<IPackageFile> GetContentFiles()
        {
            return files.Where(f => f.Path.StartsWith("content" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                                    f.Path.StartsWith("content/", StringComparison.OrdinalIgnoreCase) ||
                                    f.Path.StartsWith("content\\", StringComparison.OrdinalIgnoreCase));
        }

        public Stream GetStream()
        {
            if (!string.IsNullOrEmpty(packageFilePath) && File.Exists(packageFilePath))
            {
                return File.Open(packageFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            if (rawPackageBytes != null)
            {
                return new MemoryStream(rawPackageBytes, writable: false);
            }
            throw new InvalidOperationException("Package stream is not available.");
        }

        private void ReadPackage(Stream stream, string filePath)
        {
            if (filePath != null)
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    ZipArchiveEntry nuspecEntry = null;

                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        if (entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !entry.FullName.Contains("/") && !entry.FullName.Contains("\\"))
                        {
                            nuspecEntry = entry;
                        }

                        if (entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.EndsWith("[Content_Types].xml", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("_rels", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("package/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("package\\", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var entryFullName = entry.FullName;
                        files.Add(new ZipPackageFile(entryFullName, () =>
                        {
                            using (var za = ZipFile.OpenRead(filePath))
                            {
                                var ze = za.GetEntry(entryFullName);
                                if (ze == null)
                                    throw new FileNotFoundException($"Entry '{entryFullName}' not found in package.", entryFullName);

                                var ms = new MemoryStream();
                                using (var es = ze.Open())
                                {
                                    es.CopyTo(ms);
                                }
                                ms.Position = 0;
                                return ms;
                            }
                        }));
                    }

                    if (nuspecEntry == null)
                        throw new InvalidOperationException("Package does not contain a valid manifest (.nuspec).");

                    using (var nuspecStream = nuspecEntry.Open())
                    {
                        ReadManifest(nuspecStream);
                    }
                }
            }
            else
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    rawPackageBytes = ms.ToArray();
                }

                using (var archive = new ZipArchive(new MemoryStream(rawPackageBytes), ZipArchiveMode.Read))
                {
                    ZipArchiveEntry nuspecEntry = null;

                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        if (entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !entry.FullName.Contains("/") && !entry.FullName.Contains("\\"))
                        {
                            nuspecEntry = entry;
                        }

                        if (entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.EndsWith("[Content_Types].xml", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("_rels", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("package/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("package\\", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var entryFullName = entry.FullName;
                        files.Add(new ZipPackageFile(entryFullName, () =>
                        {
                            using (var za = new ZipArchive(new MemoryStream(rawPackageBytes), ZipArchiveMode.Read))
                            {
                                var ze = za.GetEntry(entryFullName);
                                if (ze == null)
                                    throw new FileNotFoundException($"Entry '{entryFullName}' not found in package.", entryFullName);

                                var ems = new MemoryStream();
                                using (var es = ze.Open())
                                {
                                    es.CopyTo(ems);
                                }
                                ems.Position = 0;
                                return ems;
                            }
                        }));
                    }

                    if (nuspecEntry == null)
                        throw new InvalidOperationException("Package does not contain a valid manifest (.nuspec).");

                    using (var nuspecStream = nuspecEntry.Open())
                    {
                        ReadManifest(nuspecStream);
                    }
                }
            }
        }

        private void ReadManifest(Stream stream)
        {
            var doc = XDocument.Load(stream);
            var ns = doc.Root.GetDefaultNamespace();
            var metadata = doc.Root.Element(ns + "metadata") ?? doc.Root.Element("metadata");

            if (metadata == null)
                throw new InvalidOperationException("Invalid nuspec manifest: <metadata> element not found.");

            Id = (string)metadata.Element(ns + "id") ?? (string)metadata.Element("id");
            var versionString = (string)metadata.Element(ns + "version") ?? (string)metadata.Element("version");
            Version = SemanticVersion.Parse(versionString);

            Title = (string)metadata.Element(ns + "title") ?? (string)metadata.Element("title") ?? Id;
            Description = (string)metadata.Element(ns + "description") ?? (string)metadata.Element("description");
            Summary = (string)metadata.Element(ns + "summary") ?? (string)metadata.Element("summary");
            ReleaseNotes = (string)metadata.Element(ns + "releaseNotes") ?? (string)metadata.Element("releaseNotes");

            Copyright = (string)metadata.Element(ns + "copyright") ?? (string)metadata.Element("copyright");
            Language = (string)metadata.Element(ns + "language") ?? (string)metadata.Element("language");

            var authorsStr = (string)metadata.Element(ns + "authors") ?? (string)metadata.Element("authors");
            if (!string.IsNullOrEmpty(authorsStr))
            {
                Authors = authorsStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(a => a.Trim())
                                    .ToList();
            }

            var iconUrlStr = (string)metadata.Element(ns + "iconUrl") ?? (string)metadata.Element("iconUrl");
            if (!string.IsNullOrEmpty(iconUrlStr) && Uri.TryCreate(iconUrlStr, UriKind.Absolute, out var uri))
            {
                IconUrl = uri;
            }

            var projectUrlStr = (string)metadata.Element(ns + "projectUrl") ?? (string)metadata.Element("projectUrl");
            if (!string.IsNullOrEmpty(projectUrlStr) && Uri.TryCreate(projectUrlStr, UriKind.Absolute, out var projectUri))
            {
                ProjectUrl = projectUri;
            }

            var frameworkAssembliesElem = metadata.Element(ns + "frameworkAssemblies") ?? metadata.Element("frameworkAssemblies");
            if (frameworkAssembliesElem != null)
            {
                var faElems = frameworkAssembliesElem.Elements(ns + "frameworkAssembly").Concat(frameworkAssembliesElem.Elements("frameworkAssembly"));
                foreach (var fa in faElems)
                {
                    var name = (string)fa.Attribute("assemblyName");
                    if (string.IsNullOrEmpty(name)) continue;

                    var tfStr = (string)fa.Attribute("targetFramework");
                    var tf = !string.IsNullOrEmpty(tfStr) ? ZipPackageFile.ParseFrameworkName(tfStr) : null;
                    frameworkAssemblies.Add(new FrameworkAssemblyReference(name, tf != null ? new[] { tf } : null));
                }
            }

            var dependenciesElem = metadata.Element(ns + "dependencies") ?? metadata.Element("dependencies");
            if (dependenciesElem != null)
            {
                var groups = dependenciesElem.Elements(ns + "group").Concat(dependenciesElem.Elements("group")).ToList();
                if (groups.Count > 0)
                {
                    foreach (var group in groups)
                    {
                        var tfStr = (string)group.Attribute("targetFramework");
                        var tf = !string.IsNullOrEmpty(tfStr) ? ZipPackageFile.ParseFrameworkName(tfStr) : null;
                        var deps = ParseDependencies(group, ns);
                        dependencySets.Add(new PackageDependencySet(tf, deps));
                    }
                }
                else
                {
                    var deps = ParseDependencies(dependenciesElem, ns);
                    dependencySets.Add(new PackageDependencySet(null, deps));
                }
            }
        }

        private static List<PackageDependency> ParseDependencies(XElement parent, XNamespace ns)
        {
            var list = new List<PackageDependency>();
            var depElems = parent.Elements(ns + "dependency").Concat(parent.Elements("dependency"));

            foreach (var dep in depElems)
            {
                var depId = (string)dep.Attribute("id");
                if (string.IsNullOrEmpty(depId)) continue;

                var versionSpecStr = (string)dep.Attribute("version");
                var versionSpec = !string.IsNullOrEmpty(versionSpecStr) ? VersionSpec.ParseVersionSpec(versionSpecStr) : null;
                list.Add(new PackageDependency(depId, versionSpec));
            }

            return list;
        }

        public override string ToString() => GetFullName();
    }
}
