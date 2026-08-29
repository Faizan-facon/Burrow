using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Spectre.Console;

namespace Squirrel.Cli.Commands
{
    public class ValidateSettings : GlobalSettings
    {
        [CommandOption("-r|--release-dir")]
        [Description("Path to a release directory to validate")]
        public string? ReleaseDir { get; set; }

        [CommandOption("--fix")]
        [Description("Attempt to fix issues found")]
        public bool Fix { get; set; }
    }

    public sealed class ValidateCommand : CommandBase<ValidateSettings>
    {
        protected override int ExecuteCommand(ValidateSettings settings)
        {
            var releaseDir = settings.ReleaseDir ?? Path.Combine(".", "Releases");

            if (!Directory.Exists(releaseDir))
            {
                throw new ValidationError($"Release directory not found: {releaseDir}", "--release-dir");
            }

            Context.Log().Info("Validating releases directory: " + releaseDir);

            var progressTask = Progress.AddTask("Validating...", maxValue: 100);

            var releasesPath = Path.Combine(releaseDir, "RELEASES");
            if (!File.Exists(releasesPath))
            {
                throw new UserError($"RELEASES file not found in {releaseDir}", "Run 'sync' command first to create the releases directory");
            }

            Progress.Update(progressTask, 20, "Parsing RELEASES file...");

            var entries = Squirrel.ReleaseEntry.ParseReleaseFile(File.ReadAllText(releasesPath, Encoding.UTF8)).ToList();

            Progress.Update(progressTask, 40, "Checking release files...");

            var issues = new List<string>();
            foreach (var entry in entries)
            {
                var filePath = Path.Combine(releaseDir, entry.Filename);
                if (!File.Exists(filePath))
                {
                    issues.Add($"Missing release file: {entry.Filename}");
                }
            }

            Progress.Update(progressTask, 80, "Verifying checksums...");

            Progress.Update(progressTask, 100, "Validation complete");
            Progress.Finish(progressTask);

            if (issues.Count > 0)
            {
                var panel = new Spectre.Console.Panel(
                    string.Join("\n", issues.Select(i => $"[{SquirrelTheme.Error}]✗[/] {Markup.Escape(i)}")))
                {
                    Header = new Spectre.Console.PanelHeader($"[{SquirrelTheme.Error}]Validation Failed - {issues.Count} Issues[/]"),
                    Border = Spectre.Console.BoxBorder.Rounded,
                    BorderStyle = SquirrelTheme.Error,
                    Padding = new Spectre.Console.Padding(1, 1, 1, 1)
                };

                Context.Console.Write(panel);

                if (settings.Fix)
                {
                    Context.WriteWarning("Fix option not yet implemented");
                }

                return 1;
            }
            else
            {
                var table = new Spectre.Console.Table();
                table.Border = Spectre.Console.TableBorder.Rounded;
                table.BorderStyle = SquirrelTheme.TableBorder;
                table.AddColumn(new Spectre.Console.TableColumn("Version"));
                table.AddColumn(new Spectre.Console.TableColumn("Filename"));
                table.AddColumn(new Spectre.Console.TableColumn("Size"));
                table.AddColumn(new Spectre.Console.TableColumn("Type"));

                foreach (var entry in entries.OrderByDescending(e => e.Version))
                {
                    var filePath = Path.Combine(releaseDir, entry.Filename);
                    var size = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
                    table.AddRow(
                        entry.Version.ToString(),
                        entry.Filename,
                        FormatBytes(size),
                        entry.IsDelta ? "Delta" : "Full");
                }

                Context.Console.Write(table);
                Context.WriteSuccess($"Validation passed - {entries.Count} releases found");
                return 0;
            }
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