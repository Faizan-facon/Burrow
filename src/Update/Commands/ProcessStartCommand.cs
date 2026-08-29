using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NuGet;

namespace Squirrel.Cli.Commands
{
    public class ProcessStartSettings : GlobalSettings
    {
        [CommandArgument(0, "<EXE-NAME>")]
        [Description("Executable name to start")]
        public string? ExeName { get; set; }

        [CommandOption("-a|--args")]
        [Description("Arguments to pass to the executable")]
        public string? Args { get; set; }

        [CommandOption("--wait")]
        [Description("Wait for parent process to exit before starting")]
        public bool Wait { get; set; }
    }

    public sealed class ProcessStartCommand : CommandBase<ProcessStartSettings>
    {
        protected override int ExecuteCommand(ProcessStartSettings settings)
        {
            ValidateRequired(settings.ExeName, "<EXE-NAME>", "Update.exe process-start MyApp.exe --args \"--flag\"");

            var appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var releases = Squirrel.ReleaseEntry.ParseReleaseFile(
                File.ReadAllText(Squirrel.Utility.LocalReleaseFileForAppDir(appDir), Encoding.UTF8));

            var latestAppDir = releases
                .OrderByDescending(x => x.Version)
                .SelectMany(x => new[]
                {
                    Squirrel.Utility.AppDirForRelease(appDir, x),
                    Squirrel.Utility.AppDirForVersion(appDir, new SemanticVersion(x.Version.Version.Major, x.Version.Version.Minor, x.Version.Version.Build, ""))
                })
                .FirstOrDefault(x => Directory.Exists(x));

            if (latestAppDir == null)
            {
                throw new UserError("No installed version found", "Run 'Update.exe install' first");
            }

            var targetExe = new FileInfo(Path.Combine(latestAppDir, settings.ExeName!.Replace("%20", " ")));
            Context.Log().Info("Want to launch '{0}'", targetExe);

            if (!targetExe.FullName.StartsWith(latestAppDir, StringComparison.Ordinal))
            {
                throw new UserError("Invalid executable path", "Path canonicalization attack detected");
            }

            if (!targetExe.Exists)
            {
                throw new UserError($"File {targetExe} doesn't exist in current release", $"Check that {settings.ExeName} exists in the package");
            }

            if (settings.Wait)
            {
                WaitForParentToExit();
            }

            try
            {
                Context.Log().Info("About to launch: '{0}': {1}", targetExe.FullName, settings.Args ?? "");
                Process.Start(new ProcessStartInfo(targetExe.FullName, settings.Args ?? "")
                {
                    WorkingDirectory = Path.GetDirectoryName(targetExe.FullName)
                });
            }
            catch (Exception ex)
            {
                Context.Log().ErrorException("Failed to start process", ex);
                throw new SystemError("Failed to start process", ex);
            }

            return 0;
        }

        private void WaitForParentToExit()
        {
            var parentPid = Squirrel.NativeMethods.GetParentProcessId();
            var handle = default(IntPtr);

            try
            {
                handle = Squirrel.NativeMethods.OpenProcess(Squirrel.ProcessAccess.Synchronize, false, parentPid);
                if (handle != IntPtr.Zero)
                {
                    Context.Log().Info("About to wait for parent PID {0}", parentPid);
                    Squirrel.NativeMethods.WaitForSingleObject(handle, 0xFFFFFFFF);
                }
                else
                {
                    Context.Log().Info("Parent PID {0} no longer valid - ignoring", parentPid);
                }
            }
            finally
            {
                if (handle != IntPtr.Zero) Squirrel.NativeMethods.CloseHandle(handle);
            }
        }
    }
}