using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NuGet
{
    [Serializable]
    public sealed class SemanticVersion : IComparable, IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        private const RegexOptions Flags = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
        private static readonly Regex SemanticVersionRegex = new Regex(@"^(?<Version>\d+(\s*\.\s*\d+){0,3})(?<Release>-[a-z][0-9a-z-]*)?$", Flags);
        private static readonly Regex StrictSemanticVersionRegex = new Regex(@"^(?<Version>\d+(\.\d+){2})(?<Release>-[a-z0-9-]+(\.[a-z0-9-]+)*)?(?<Metadata>\+[a-z0-9-]+(\.[a-z0-9-]+)*)?$", Flags);

        private readonly string originalString;

        public SemanticVersion(string version)
        {
            var semVer = Parse(version);
            Version = semVer.Version;
            SpecialVersion = semVer.SpecialVersion;
            originalString = version;
        }

        public SemanticVersion(int major, int minor, int build, int revision)
            : this(new Version(major, minor, build, revision), "")
        {
        }

        public SemanticVersion(int major, int minor, int build, string specialVersion)
            : this(new Version(major, minor, Math.Max(build, 0)), specialVersion)
        {
        }

        public SemanticVersion(Version version, string specialVersion = null)
            : this(version, specialVersion, null)
        {
        }

        private SemanticVersion(Version version, string specialVersion, string originalString)
        {
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            Version = NormalizeVersionValue(version);
            SpecialVersion = specialVersion ?? string.Empty;
            this.originalString = originalString;
        }

        public Version Version { get; }
        public string SpecialVersion { get; }

        public int Major => Version.Major;
        public int Minor => Version.Minor;
        public int Patch => Math.Max(0, Version.Build);
        public int Build => Math.Max(0, Version.Build);
        public int Revision => Math.Max(0, Version.Revision);

        public static SemanticVersion Parse(string version)
        {
            if (string.IsNullOrEmpty(version))
                throw new ArgumentException("Version string cannot be null or empty", nameof(version));

            if (!TryParse(version, out var semVer))
                throw new FormatException($"'{version}' is not a valid version string.");

            return semVer;
        }

        public static bool TryParse(string version, out SemanticVersion value)
        {
            value = null;
            if (string.IsNullOrEmpty(version))
                return false;

            var match = SemanticVersionRegex.Match(version.Trim());
            if (!match.Success)
                return false;

            if (!Version.TryParse(match.Groups["Version"].Value, out var parsedVersion))
                return false;

            parsedVersion = NormalizeVersionValue(parsedVersion);
            var specialVersion = match.Groups["Release"].Value.TrimStart('-');

            value = new SemanticVersion(parsedVersion, specialVersion, version);
            return true;
        }

        public static bool TryParseStrict(string version, out SemanticVersion value)
        {
            value = null;
            if (string.IsNullOrEmpty(version))
                return false;

            var match = StrictSemanticVersionRegex.Match(version.Trim());
            if (!match.Success)
                return false;

            if (!Version.TryParse(match.Groups["Version"].Value, out var parsedVersion))
                return false;

            parsedVersion = NormalizeVersionValue(parsedVersion);
            var specialVersion = match.Groups["Release"].Value.TrimStart('-');

            value = new SemanticVersion(parsedVersion, specialVersion, version);
            return true;
        }

        private static Version NormalizeVersionValue(Version version)
        {
            return new Version(
                version.Major,
                version.Minor,
                Math.Max(version.Build, 0),
                Math.Max(version.Revision, 0));
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(originalString))
                return originalString;

            var versionString = Version.Revision > 0
                ? Version.ToString(4)
                : Version.Build > 0
                    ? Version.ToString(3)
                    : Version.ToString(2);

            return string.IsNullOrEmpty(SpecialVersion)
                ? versionString
                : $"{versionString}-{SpecialVersion}";
        }

        public override bool Equals(object obj)
        {
            return obj is SemanticVersion other && Equals(other);
        }

        public bool Equals(SemanticVersion other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Version.Equals(other.Version) && string.Equals(SpecialVersion, other.SpecialVersion, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Version.GetHashCode() * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(SpecialVersion ?? string.Empty);
            }
        }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;
            if (obj is SemanticVersion other) return CompareTo(other);
            throw new ArgumentException("Object must be of type SemanticVersion.", nameof(obj));
        }

        public int CompareTo(SemanticVersion other)
        {
            if (ReferenceEquals(null, other)) return 1;
            if (ReferenceEquals(this, other)) return 0;

            int versionCompare = Version.CompareTo(other.Version);
            if (versionCompare != 0) return versionCompare;

            bool thisEmpty = string.IsNullOrEmpty(SpecialVersion);
            bool otherEmpty = string.IsNullOrEmpty(other.SpecialVersion);

            if (thisEmpty && otherEmpty) return 0;
            if (thisEmpty) return 1; // Release > Prerelease
            if (otherEmpty) return -1; // Prerelease < Release

            return CompareSpecialVersion(SpecialVersion, other.SpecialVersion);
        }

        private static int CompareSpecialVersion(string version1, string version2)
        {
            if (string.Equals(version1, version2, StringComparison.OrdinalIgnoreCase))
                return 0;

            var parts1 = version1.Split('.');
            var parts2 = version2.Split('.');

            int count = Math.Min(parts1.Length, parts2.Length);
            for (int i = 0; i < count; i++)
            {
                int cmp = CompareComponent(parts1[i], parts2[i]);
                if (cmp != 0) return cmp;
            }

            return parts1.Length.CompareTo(parts2.Length);
        }

        private static int CompareComponent(string a, string b)
        {
            if (int.TryParse(a, out int numA) && int.TryParse(b, out int numB))
            {
                return numA.CompareTo(numB);
            }

            int idxA = a.Length - 1;
            while (idxA >= 0 && char.IsDigit(a[idxA])) idxA--;
            idxA++;

            int idxB = b.Length - 1;
            while (idxB >= 0 && char.IsDigit(b[idxB])) idxB--;
            idxB++;

            if (idxA > 0 && idxB > 0 && idxA < a.Length && idxB < b.Length)
            {
                string prefixA = a.Substring(0, idxA);
                string prefixB = b.Substring(0, idxB);

                int prefixCmp = StringComparer.OrdinalIgnoreCase.Compare(prefixA, prefixB);
                if (prefixCmp != 0) return prefixCmp;

                if (long.TryParse(a.Substring(idxA), out long valA) && long.TryParse(b.Substring(idxB), out long valB))
                {
                    return valA.CompareTo(valB);
                }
            }

            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }

        public static bool operator ==(SemanticVersion left, SemanticVersion right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SemanticVersion left, SemanticVersion right)
        {
            return !Equals(left, right);
        }

        public static bool operator <(SemanticVersion left, SemanticVersion right)
        {
            return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(SemanticVersion left, SemanticVersion right)
        {
            return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(SemanticVersion left, SemanticVersion right)
        {
            return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(SemanticVersion left, SemanticVersion right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
        }
    }
}
