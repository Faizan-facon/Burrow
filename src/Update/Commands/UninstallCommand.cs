using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Squirrel.Cli.Commands
{
    public class UninstallSettings : GlobalSettings
    {
        [CommandOption("--app-name <NAME>")]
        [Description("Application name (defaults to directory name)")]
        public string AppName { get; set; }
    }

    public sealed class UninstallCommand : CommandBase<UninstallSettings>
    {
        protected override int ExecuteCommand(UninstallSettings settings)
        {
            var appName = settings.AppName ?? GetAppNameFromDirectory();

            Context.Log().Info("Starting uninstall for app: " + appName);

            using (var mgr = new Squirrel.UpdateManager("", appName))
            {
                var progressTask = Progress.AddTask("Uninstalling application...", maxValue: 100);

                mgr.FullUninstall().Wait();
                Progress.Update(progressTask, 50);

                mgr.RemoveUninstallerRegistryEntry();
                Progress.Update(progressTask, 100);

                Progress.Finish(progressTask);
            }

            Context.WriteSuccess($"Application uninstalled");
            return 0;
        }

        private string GetAppNameFromDirectory(string path = null)
        {
            path = path ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return new DirectoryInfo(path).Name;
        }
    }
}