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
    public class CheckForUpdateSettings : GlobalSettings
    {
        [CommandArgument(0, "<URL>")]
        [Description("Update URL to check")]
        public string? Url { get; set; }

        [CommandOption("--app-name")]
        [Description("Application name (defaults to directory name)")]
        public string? AppName { get; set; }
    }

    public sealed class CheckForUpdateCommand : CommandBase<CheckForUpdateSettings>
    {
        protected override int ExecuteCommand(CheckForUpdateSettings settings)
        {
            ValidateRequired(settings.Url, "--url", "Update.exe check-update --url https://example.com/updates");

            var appName = settings.AppName ?? GetAppNameFromDirectory();

            Context.Log().Info("Fetching update information, downloading from " + settings.Url);

            using (var mgr = new Squirrel.UpdateManager(settings.Url, appName))
            {
                var progressTask = Progress.AddTask("Checking for updates...", maxValue: 100);

                var updateInfo = mgr.CheckForUpdate(intention: Squirrel.UpdaterIntention.Update, progress: x =>
                {
                    Progress.Update(progressTask, x);
                }).Result;

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

                if (updateInfo.ReleasesToApply.Count > 0)
                {
                    Context.WriteInfo($"Update available: {updateInfo.FutureReleaseEntry.Version}");
                    return 4; // Update available
                }
                else
                {
                    Context.WriteInfo("No update available");
                    return 5; // No update
                }
            }
        }

        private string GetAppNameFromDirectory(string? path = null)
        {
            path = path ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return new DirectoryInfo(path).Name;
        }
    }
}