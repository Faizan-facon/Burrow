using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Squirrel.Cli;
using Squirrel.Cli.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Squirrel.Update
{
    class Program
    {
        static int Main(string[] args)
        {
            // Handle legacy syntax before Spectre.Console.Cli processes
            args = HandleLegacySyntax(args);

            // Handle squirrel-aware events
            if (args.Any(x => x.StartsWith("/squirrel", StringComparison.OrdinalIgnoreCase)))
            {
                return 0;
            }

            var app = new CommandApp();
            app.Configure(config =>
            {
                config.SetApplicationName("Update.exe");
                config.SetApplicationVersion(Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0");

                // Add commands
                config.AddCommand<InstallCommand>("install")
                    .WithDescription("Install the app from a package directory");

                config.AddCommand<UninstallCommand>("uninstall")
                    .WithDescription("Uninstall the app");

                config.AddCommand<DownloadCommand>("download")
                    .WithDescription("Download releases and output JSON");

                config.AddCommand<CheckForUpdateCommand>("check-update")
                    .WithDescription("Check for available updates");

                config.AddCommand<UpdateCommand>("update")
                    .WithDescription("Update to latest version");

                config.AddCommand<ShortcutCommand>("shortcut")
                    .WithDescription("Create a shortcut for the given executable");

                config.AddCommand<RemoveShortcutCommand>("remove-shortcut")
                    .WithDescription("Remove a shortcut for the given executable");

                config.AddCommand<UpdateSelfCommand>("update-self")
                    .WithDescription("Self-update Update.exe");

                config.AddCommand<ProcessStartCommand>("process-start")
                    .WithDescription("Start an executable in the latest version of the app package");

                // Global options are defined in GlobalSettings base class
                config.PropagateExceptions();
                config.ValidateExamples();
            });

            return app.Run(args);
        }

        private static string[] HandleLegacySyntax(string[] args)
        {
            // Map legacy short flags to new commands
            if (args.Length < 2)
            {
                return args;
            }

            var newArgs = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--install":
                        newArgs.Add("install");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--uninstall":
                        newArgs.Add("uninstall");
                        break;
                    case "--download":
                        newArgs.Add("download");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add("--url");
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--checkforupdate":
                    case "--check-for-update":
                        newArgs.Add("check-update");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add("--url");
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--update":
                        newArgs.Add("update");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add("--url");
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--releasify":
                        newArgs.Add("releasify");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--createshortcut":
                    case "--create-shortcut":
                        newArgs.Add("shortcut");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--removeshortcut":
                    case "--remove-shortcut":
                        newArgs.Add("remove-shortcut");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    case "--updateself":
                    case "--update-self":
                        newArgs.Add("update-self");
                        break;
                    case "--processstart":
                    case "--process-start":
                        newArgs.Add("process-start");
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            newArgs.Add(args[++i]);
                        }
                        break;
                    default:
                        newArgs.Add(args[i]);
                        break;
                }
            }

            if (!newArgs.SequenceEqual(args))
            {
                // Show deprecation warning
                Console.Error.WriteLine($"[DEPRECATION] Legacy syntax detected. Please use the new command syntax.");
                return newArgs.ToArray();
            }

            return args;
        }
    }
}