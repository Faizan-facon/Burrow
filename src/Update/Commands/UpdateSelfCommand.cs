using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Squirrel.Cli.Commands
{
    public class UpdateSelfSettings : GlobalSettings
    {
        [CommandOption("--target")]
        [Description("Target path for self-update")]
        public string? Target { get; set; }
    }

    public sealed class UpdateSelfCommand : CommandBase<UpdateSelfSettings>
    {
        protected override int ExecuteCommand(UpdateSelfSettings settings)
        {
            WaitForParentToExit();

            var src = Assembly.GetExecutingAssembly().Location;
            var target = settings.Target ?? Path.Combine(
                Path.GetDirectoryName(src),
                "..", "Update.exe");

            Task.Run(() => File.Copy(src, target, true)).Wait();

            Context.WriteSuccess($"Self-updated to {target}");
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