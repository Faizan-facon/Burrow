using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Squirrel.Cli.Commands
{
    public class UpdateSettings : GlobalSettings
    {
        [CommandArgument(0, "[URL]")]
        [Description("Update URL")]
        public string UrlArg { get; set; }

        [CommandOption("-u|--url <URL>")]
        [Description("Update URL")]
        public string Url { get; set; }

        [CommandOption("--app-name <NAME>")]
        [Description("Application name (defaults to directory name)")]
        public string AppName { get; set; }

        [CommandOption("--ignore-delta")]
        [Description("Ignore delta updates and use full updates")]
        public bool IgnoreDelta { get; set; }

        public string GetEffectiveUrl() => Url ?? UrlArg;
    }

    public sealed class UpdateCommand : CommandBase<UpdateSettings>
    {
        protected override int ExecuteCommand(UpdateSettings settings)
        {
            var url = settings.GetEffectiveUrl();
            ValidateRequired(url, "--url", "Update.exe update --url https://example.com/updates");

            if (!url!.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationError("Invalid repository URL. Must start with http, https, or file://", "--url");
            }

            var appName = settings.AppName ?? GetAppNameFromDirectory();

            Context.Log().Info("Starting update, downloading from " + url);

            using (var mgr = new Squirrel.UpdateManager(url, appName))
            {
                Squirrel.UpdateInfo updateInfo = null;
                bool ignoreDeltaUpdates = settings.IgnoreDelta;

                var progressTask = Progress.AddTask("Checking for updates...", maxValue: 100);

                retry:
                try
                {
                    updateInfo = mgr.CheckForUpdate(
                        intention: Squirrel.UpdaterIntention.Update,
                        ignoreDeltaUpdates: ignoreDeltaUpdates,
                        progress: x => Progress.Update(progressTask, x * 0.03)).Result;

                    Progress.Update(progressTask, 3, "Downloading releases...");

                    mgr.DownloadReleases(updateInfo.ReleasesToApply, x =>
                    {
                        Progress.Update(progressTask, 3 + (x * 0.27));
                    }).Wait();

                    Progress.Update(progressTask, 30, "Applying releases...");

                    mgr.ApplyReleases(updateInfo, x =>
                    {
                        Progress.Update(progressTask, 30 + (x * 0.70));
                    }).Wait();
                }
                catch (Exception ex)
                {
                    if (ignoreDeltaUpdates)
                    {
                        Context.Log().ErrorException("Really couldn't apply updates!", ex);
                        throw;
                    }

                    Context.Log().WarnException("Failed to apply updates, falling back to full updates", ex);
                    ignoreDeltaUpdates = true;
                    goto retry;
                }

                Progress.Finish(progressTask);

                var updateTarget = Path.Combine(mgr.RootAppDirectory, "Update.exe");

                mgr.CreateUninstallerRegistryEntry().Wait();

                Context.WriteSuccess($"Application updated to {updateInfo.FutureReleaseEntry.Version}");
            }

            return 0;
        }

        private string GetAppNameFromDirectory(string path = null)
        {
            path = path ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return new DirectoryInfo(path).Name;
        }
    }
}