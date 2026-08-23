using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace NuGet
{
    public interface IVersionSpec
    {
        SemanticVersion MinVersion { get; }
        bool IsMinInclusive { get; }
        SemanticVersion MaxVersion { get; }
        bool IsMaxInclusive { get; }
    }

    public class VersionSpec : IVersionSpec
    {
        public VersionSpec()
        {
        }

        public VersionSpec(SemanticVersion minVersion)
        {
            MinVersion = minVersion;
            IsMinInclusive = true;
        }

        public SemanticVersion MinVersion { get; set; }
        public bool IsMinInclusive { get; set; }
        public SemanticVersion MaxVersion { get; set; }
        public bool IsMaxInclusive { get; set; }

        public static IVersionSpec ParseVersionSpec(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var versionSpec = new VersionSpec();
            value = value.Trim();

            // Check if exact version like [1.0]
            if (value.StartsWith("[") && value.EndsWith("]") && !value.Contains(","))
            {
                var version = SemanticVersion.Parse(value.Substring(1, value.Length - 2));
                versionSpec.MinVersion = version;
                versionSpec.IsMinInclusive = true;
                versionSpec.MaxVersion = version;
                versionSpec.IsMaxInclusive = true;
                return versionSpec;
            }

            // Check if range like (1.0, 2.0] or [1.0, )
            if ((value.StartsWith("(") || value.StartsWith("[")) && (value.EndsWith(")") || value.EndsWith("]")))
            {
                versionSpec.IsMinInclusive = value.StartsWith("[");
                versionSpec.IsMaxInclusive = value.EndsWith("]");

                var inner = value.Substring(1, value.Length - 2);
                var parts = inner.Split(',');

                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    versionSpec.MinVersion = SemanticVersion.Parse(parts[0].Trim());
                }

                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    versionSpec.MaxVersion = SemanticVersion.Parse(parts[1].Trim());
                }

                return versionSpec;
            }

            // Simple version string, treated as >= version
            if (SemanticVersion.TryParse(value, out var parsed))
            {
                versionSpec.MinVersion = parsed;
                versionSpec.IsMinInclusive = true;
                return versionSpec;
            }

            return versionSpec;
        }

        public override string ToString()
        {
            if (MinVersion != null && MaxVersion != null && MinVersion == MaxVersion && IsMinInclusive && IsMaxInclusive)
                return $"[{MinVersion}]";

            if (MinVersion != null && MaxVersion == null && IsMinInclusive)
                return MinVersion.ToString();

            var sb = new StringBuilder();
            sb.Append(IsMinInclusive ? '[' : '(');
            if (MinVersion != null) sb.Append(MinVersion);
            sb.Append(", ");
            if (MaxVersion != null) sb.Append(MaxVersion);
            sb.Append(IsMaxInclusive ? ']' : ')');
            return sb.ToString();
        }
    }

    public class PackageDependency
    {
        public PackageDependency(string id, IVersionSpec versionSpec = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));

            Id = id;
            VersionSpec = versionSpec;
        }

        public string Id { get; }
        public IVersionSpec VersionSpec { get; }

        public override string ToString()
        {
            return VersionSpec != null ? $"{Id} {VersionSpec}" : Id;
        }
    }

    public class PackageDependencySet
    {
        public PackageDependencySet(FrameworkName targetFramework, IEnumerable<PackageDependency> dependencies)
        {
            TargetFramework = targetFramework;
            Dependencies = new ReadOnlyCollection<PackageDependency>((dependencies ?? Enumerable.Empty<PackageDependency>()).ToList());
        }

        public FrameworkName TargetFramework { get; }
        public ICollection<PackageDependency> Dependencies { get; }
    }

    public static class VersionUtility
    {
        public static IVersionSpec ParseVersionSpec(string value)
        {
            return VersionSpec.ParseVersionSpec(value);
        }

        public static bool IsCompatible(FrameworkName projectFramework, IEnumerable<FrameworkName> targetFrameworks)
        {
            if (projectFramework == null || targetFrameworks == null)
                return true;

            var list = targetFrameworks.Where(x => x != null).ToList();
            if (list.Count == 0)
                return true;

            foreach (var tf in list)
            {
                if (string.Equals(tf.Identifier, projectFramework.Identifier, StringComparison.OrdinalIgnoreCase) &&
                    tf.Version <= projectFramework.Version)
                {
                    return true;
                }

                if (string.Equals(projectFramework.Identifier, ".NETFramework", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(tf.Identifier, ".NETFramework-Client", StringComparison.OrdinalIgnoreCase) && tf.Version <= projectFramework.Version)
                        return true;

                    if (string.Equals(tf.Identifier, ".NETPortable", StringComparison.OrdinalIgnoreCase))
                    {
                        var profile = tf.Profile?.ToLowerInvariant() ?? "";
                        if (profile.Contains("net45") && projectFramework.Version >= new Version(4, 5))
                            return true;
                        if (profile.Contains("net40") && projectFramework.Version >= new Version(4, 0))
                            return true;
                        if (profile.Contains("net35") && projectFramework.Version >= new Version(3, 5))
                            return true;
                        if (profile.Contains("net") && !profile.Contains("netcore"))
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
