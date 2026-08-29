using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Squirrel.Cli.Commands
{
    public class DownloadSettings : GlobalSettings
    {
        [CommandArgument(0, "<URL>")]
        [Description("Update URL to download from")]
        public string? Url { get; set; }

        [CommandOption("--app-name")]
        [Description("Application name (defaults to directory name)")]
        public string? AppName { get; set; }
    }

    public sealed class DownloadCommand : CommandBase<DownloadSettings>
    {
        protected override int ExecuteCommand(DownloadSettings settings)
        {
            ValidateRequired(settings.Url, "--url", "Update.exe download --url https://example.com/updates");

            var appName = settings.AppName ?? GetAppNameFromDirectory();

            Context.Log().Info("Fetching update information, downloading from " + settings.Url);

            using (var mgr = new Squirrel.UpdateManager(settings.Url, appName))
            {
                var progressTask = Progress.AddTask("Checking for updates...", maxValue: 100);

                var updateInfo = mgr.CheckForUpdate(intention: Squirrel.UpdaterIntention.Update, progress: x =>
                {
                    Progress.Update(progressTask, x);
                }).Result;

                Progress.Update(progressTask, 33, "Downloading releases...");

                mgr.DownloadReleases(updateInfo.ReleasesToApply, x =>
                {
                    Progress.Update(progressTask, 33 + (x * 0.67));
                }).Wait();

                Progress.Finish(progressTask);

                var releaseNotes = updateInfo.FetchReleaseNotes();

                var sanitizedUpdateInfo = new
                {
                    currentVersion = updateInfo.CurrentlyInstalledVersion.Version.ToString(),
                    futureVersion = updateInfo.FutureReleaseEntry.Version.ToString(),
                    releasesToApply = updateInfo.ReleasesToApply.Select(x => new
                    {
                        version = x.Version.ToString(),
                        releaseNotes = releaseNotes.ContainsKey(x) ? releaseNotes[x] : "",
                    }).ToArray(),
                };

                Output.Write(sanitizedUpdateInfo);
            }

            return 0;
        }

        private string GetAppNameFromDirectory(string? path = null)
        {
            path = path ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return new DirectoryInfo(path).Name;
        }
    }
}