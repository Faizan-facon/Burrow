using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Squirrel.Cli.Commands
{
    public class ListSettings : GlobalSettings
    {
        [CommandOption("-r|--release-dir <DIR>")]
        [Description("Path to a release directory to list")]
        public string ReleaseDir { get; set; }

        [CommandOption("--show-deltas <VALUE>")]
        [Description("Include delta packages in output")]
        public bool ShowDeltas { get; set; } = true;
    }

    public sealed class ListCommand : CommandBase<ListSettings>
    {
        protected override int ExecuteCommand(ListSettings settings)
        {
            var releaseDir = settings.ReleaseDir ?? (Directory.Exists(Path.Combine(".", "Releases")) ? Path.Combine(".", "Releases") : ".");

            if (!Directory.Exists(releaseDir))
            {
                throw new ValidationError($"Release directory not found: {releaseDir}", "--release-dir");
            }

            var releasesPath = Path.Combine(releaseDir, "RELEASES");
            if (!File.Exists(releasesPath))
            {
                throw new UserError($"RELEASES file not found in {releaseDir}", "Run 'sync' command first to create the releases directory");
            }

            var entries = Squirrel.ReleaseEntry.ParseReleaseFile(File.ReadAllText(releasesPath, Encoding.UTF8))
                .Where(e => settings.ShowDeltas || !e.IsDelta)
                .OrderByDescending(e => e.Version)
                .ToList();

            var outputData = entries.Select(e =>
            {
                var filePath = Path.Combine(releaseDir, e.Filename);
                var size = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
                return new
                {
                    version = e.Version.ToString(),
                    filename = e.Filename,
                    size = FormatBytes(size),
                    type = e.IsDelta ? "Delta" : "Full",
                    url = (e.BaseUrl ?? "") + e.Filename
                };
            }).ToList();

            Output.Write(outputData);
            return 0;
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int i = 0;
            double value = bytes;
            while (value >= 1024 && i < suffixes.Length - 1)
            {
                value /= 1024;
                i++;
            }
            return $"{value:F1} {suffixes[i]}";
        }
    }
}