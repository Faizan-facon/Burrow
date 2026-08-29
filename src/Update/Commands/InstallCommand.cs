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
    public class InstallSettings : GlobalSettings
    {
        [CommandArgument(0, "[PATH]")]
        [Description("Path to the package directory")]
        public string PathArg { get; set; }

        [CommandOption("-p|--path <PATH>")]
        [Description("Path to the package directory")]
        public string Path { get; set; }

        [CommandOption("-s|--silent")]
        [Description("Silent install")]
        public bool Silent { get; set; }

        [CommandOption("--app-name <NAME>")]
        [Description("Application name (defaults to directory name)")]
        public string AppName { get; set; }

        public string GetEffectivePath() => Path ?? PathArg;
    }

    public sealed class InstallCommand : CommandBase<InstallSettings>
    {
        protected override int ExecuteCommand(InstallSettings settings)
        {
            var path = settings.GetEffectivePath();
            ValidateRequired(path, "--path", "Update.exe install --path ./packages");

            if (!Directory.Exists(path))
            {
                throw new ValidationError($"Directory not found: {path}", "--path");
            }

            var sourceDirectory = Path.GetFullPath(path);
            var releasesPath = Path.Combine(sourceDirectory, "RELEASES");

            Context.Log().Info("Starting install, writing to {0}", sourceDirectory);

            if (!File.Exists(releasesPath))
            {
                Context.Log().Info("RELEASES doesn't exist, creating it at " + releasesPath);
                var nupkgs = new DirectoryInfo(sourceDirectory).GetFiles()
                    .Where(x => x.Name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                    .Select(x => Squirrel.ReleaseEntry.GenerateFromFile(x.FullName));

                Squirrel.ReleaseEntry.WriteReleaseFile(nupkgs, releasesPath);
            }

            var ourAppName = Squirrel.ReleaseEntry.ParseReleaseFile(File.ReadAllText(releasesPath, Encoding.UTF8))
                .First().PackageName;

            using (var mgr = new Squirrel.UpdateManager(sourceDirectory, ourAppName))
            {
                Context.Log().Info("About to install to: " + mgr.RootAppDirectory);

                if (Directory.Exists(mgr.RootAppDirectory))
                {
                    Context.Log().Warn("Install path {0} already exists, burning it to the ground", mgr.RootAppDirectory);
                    mgr.KillAllExecutablesBelongingToPackage();
                    Task.Delay(500).Wait();

                    Squirrel.Utility.DeleteDirectory(mgr.RootAppDirectory);
                    Squirrel.Utility.Retry(() => Directory.CreateDirectory(mgr.RootAppDirectory), 3);
                }

                Directory.CreateDirectory(mgr.RootAppDirectory);

                var updateTarget = Path.Combine(mgr.RootAppDirectory, "Update.exe");
                File.Copy(Assembly.GetExecutingAssembly().Location, updateTarget, true);

                var progressTask = Progress.AddTask("Installing application...", maxValue: 100);

                mgr.FullInstall(settings.Silent, p => Progress.Update(progressTask, p)).Wait();

                Progress.Finish(progressTask);

                mgr.CreateUninstallerRegistryEntry();

                var installResult = new[]
                {
                    new
                    {
                        App = ourAppName,
                        Path = mgr.RootAppDirectory,
                        Status = "Installed"
                    }
                };

                Output.Write(installResult);

                if (Context.OutputFormat != OutputFormat.Json && Context.OutputFormat != OutputFormat.Table)
                {
                    Context.WriteSuccess($"Application installed to {mgr.RootAppDirectory}");
                }
            }

            return 0;
        }
    }
}