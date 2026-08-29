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
    public class SyncSettings : GlobalSettings
    {
        [CommandOption("-r|--release-dir")]
        [Description("Path to a release directory to download to")]
        public string? ReleaseDir { get; set; }

        [CommandOption("-u|--url")]
        [Description("GitHub repository URL or remote Releases folder URL")]
        public string? Url { get; set; }

        [CommandOption("-t|--token")]
        [Description("OAuth token for GitHub authentication")]
        public string? Token { get; set; }

        [CommandOption("--dry-run")]
        [Description("Show what would be done without writing")]
        public bool DryRun { get; set; }

        [CommandOption("--parallel")]
        [Description("Number of parallel downloads")]
        public int Parallel { get; set; } = 4;
    }

    public sealed class SyncCommand : CommandBase<SyncSettings>
    {
        protected override int ExecuteCommand(SyncSettings settings)
        {
            ValidateRequired(settings.Url, "--url", "SyncReleases.exe sync --url https://github.com/owner/repo --release-dir ./Releases");

            if (!settings.Url!.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationError("Invalid repository URL. Must start with http or https", "--url");
            }

            var releaseDirectoryInfo = new DirectoryInfo(settings.ReleaseDir ?? Path.Combine(".", "Releases"));
            if (!releaseDirectoryInfo.Exists)
            {
                releaseDirectoryInfo.Create();
            }

            if (settings.DryRun)
            {
                ShowDryRunPanel(settings.Url, releaseDirectoryInfo.FullName, settings.Token != null, settings.Parallel);
                if (!CliPrompts.Confirm(Context.Console, "Proceed with sync?", true))
                {
                    Context.WriteInfo("Sync cancelled");
                    return 0;
                }
            }

            var progressTask = Progress.AddTask("Syncing releases...", maxValue: 100);

            var githubException = default(Exception);
            try
            {
                Context.Log().Info("Attempting to sync from GitHub: " + settings.Url);
                Progress.Update(progressTask, 10, "Connecting to GitHub...");

                SyncReleases.SyncImplementations.SyncFromGitHub(settings.Url, settings.Token, releaseDirectoryInfo).Wait();

                Progress.Update(progressTask, 100, "Sync complete");
                Progress.Finish(progressTask);

                Context.WriteSuccess($"Releases synced to {releaseDirectoryInfo.FullName}");
                return 0;
            }
            catch (Exception ex)
            {
                githubException = ex;
                Context.Log().Warn("Attempting to sync URL as remote RELEASES folder: {0}", ex.Message);
            }

            try
            {
                Progress.Update(progressTask, 20, "Trying remote RELEASES folder...");
                SyncReleases.SyncImplementations.SyncRemoteReleases(new Uri(settings.Url!), releaseDirectoryInfo).Wait();

                Progress.Update(progressTask, 100, "Sync complete");
                Progress.Finish(progressTask);

                Context.WriteSuccess($"Releases synced to {releaseDirectoryInfo.FullName}");
                return 0;
            }
            catch (Exception ex)
            {
                Context.Log().Error("Failed to sync URL as GitHub repo: {0}", githubException?.Message);
                throw new SystemError($"Failed to sync: {ex.Message}", ex, "Check the URL and token, and ensure network connectivity");
            }
        }

        private void ShowDryRunPanel(string url, string releaseDir, bool hasToken, int parallel)
        {
            var panel = new Spectre.Console.Panel(
                $"[yellow]Source:[/] {Markup.Escape(url)}\n" +
                $"[yellow]Destination:[/] {Markup.Escape(releaseDir)}\n" +
                $"[yellow]Authentication:[/] {(hasToken ? "[green]Token provided[/]" : "[yellow]None (public only)[/]")}\n" +
                $"[yellow]Parallel downloads:[/] {parallel}\n" +
                $"[red]Mode:[/] [yellow]DRY RUN - No files will be written[/]")
            {
                Header = new Spectre.Console.PanelHeader($"[blue]SyncReleases - Dry Run Preview[/]"),
                Border = Spectre.Console.BoxBorder.Rounded,
                BorderStyle = Style.Parse("yellow"),
                Padding = new Spectre.Console.Padding(1, 1, 1, 1)
            };

            Context.Console.Write(panel);
        }
    }
}