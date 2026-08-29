using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace Squirrel.Cli.Commands
{
    public class ShortcutSettings : GlobalSettings
    {
        [CommandArgument(0, "<EXE-NAME>")]
        [Description("Executable name to create shortcut for")]
        public string ExeName { get; set; }

        [CommandOption("-l|--shortcut-locations <LOCATIONS>")]
        [Description("Comma-separated string of shortcut locations (e.g. 'Desktop,StartMenu')")]
        public string ShortcutLocations { get; set; }

        [CommandOption("-a|--process-start-args <ARGS>")]
        [Description("Arguments to use when starting executable")]
        public string ProcessStartArgs { get; set; }

        [CommandOption("--icon <ICO>")]
        [Description("Path to an ICO file for the shortcut")]
        public string Icon { get; set; }

        [CommandOption("--update-only")]
        [Description("Update shortcuts that already exist, rather than creating new ones")]
        public bool UpdateOnly { get; set; }
    }

    public sealed class ShortcutCommand : CommandBase<ShortcutSettings>
    {
        protected override int ExecuteCommand(ShortcutSettings settings)
        {
            ValidateRequired(settings.ExeName, "<EXE-NAME>", "Update.exe shortcut MyApp.exe");

            var appName = GetAppNameFromDirectory();
            var defaultLocations = Squirrel.ShortcutLocation.StartMenu | Squirrel.ShortcutLocation.Desktop;
            var locations = ParseShortcutLocations(settings.ShortcutLocations);

            using (var mgr = new Squirrel.UpdateManager("", appName))
            {
                mgr.CreateShortcutsForExecutable(settings.ExeName!, locations ?? defaultLocations, settings.UpdateOnly, settings.ProcessStartArgs, settings.Icon);
            }

            if (Context.OutputFormat == OutputFormat.Json)
            {
                Output.Write(new
                {
                    success = true,
                    exeName = settings.ExeName,
                    action = "created"
                });
            }
            else
            {
                Context.WriteSuccess($"Shortcut created for {settings.ExeName}");
            }
            return 0;
        }

        private string GetAppNameFromDirectory(string path = null)
        {
            path = path ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return new DirectoryInfo(path).Name;
        }

        private Squirrel.ShortcutLocation? ParseShortcutLocations(string shortcutArgs)
        {
            if (String.IsNullOrWhiteSpace(shortcutArgs)) return null;

            var ret = default(Squirrel.ShortcutLocation?);
            var args = shortcutArgs.Split(new[] { ',' });

            foreach (var arg in args)
            {
                var location = (Squirrel.ShortcutLocation)(Enum.Parse(typeof(Squirrel.ShortcutLocation), arg, false));
                if (ret.HasValue)
                {
                    ret |= location;
                }
                else
                {
                    ret = location;
                }
            }

            return ret;
        }
    }

    public class RemoveShortcutSettings : GlobalSettings
    {
        [CommandArgument(0, "<EXE-NAME>")]
        [Description("Executable name to remove shortcut for")]
        public string ExeName { get; set; }

        [CommandOption("-l|--shortcut-locations <LOCATIONS>")]
        [Description("Comma-separated string of shortcut locations (e.g. 'Desktop,StartMenu')")]
        public string ShortcutLocations { get; set; }
    }

    public sealed class RemoveShortcutCommand : CommandBase<RemoveShortcutSettings>
    {
        protected override int ExecuteCommand(RemoveShortcutSettings settings)
        {
            ValidateRequired(settings.ExeName, "<EXE-NAME>", "Update.exe remove-shortcut MyApp.exe");

            var appName = GetAppNameFromDirectory();
            var defaultLocations = Squirrel.ShortcutLocation.StartMenu | Squirrel.ShortcutLocation.Desktop;
            var locations = ParseShortcutLocations(settings.ShortcutLocations);

            using (var mgr = new Squirrel.UpdateManager("", appName))
            {
                mgr.RemoveShortcutsForExecutable(settings.ExeName!, locations ?? defaultLocations);
            }

            if (Context.OutputFormat == OutputFormat.Json)
            {
                Output.Write(new
                {
                    success = true,
                    exeName = settings.ExeName,
                    action = "removed"
                });
            }
            else
            {
                Context.WriteSuccess($"Shortcut removed for {settings.ExeName}");
            }
            return 0;
        }

        private string GetAppNameFromDirectory(string path = null)
        {
            path = path ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return new DirectoryInfo(path).Name;
        }

        private Squirrel.ShortcutLocation? ParseShortcutLocations(string shortcutArgs)
        {
            if (String.IsNullOrWhiteSpace(shortcutArgs)) return null;

            var ret = default(Squirrel.ShortcutLocation?);
            var args = shortcutArgs.Split(new[] { ',' });

            foreach (var arg in args)
            {
                var location = (Squirrel.ShortcutLocation)(Enum.Parse(typeof(Squirrel.ShortcutLocation), arg, false));
                if (ret.HasValue)
                {
                    ret |= location;
                }
                else
                {
                    ret = location;
                }
            }

            return ret;
        }
    }
}