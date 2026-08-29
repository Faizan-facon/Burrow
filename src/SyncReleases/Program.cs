using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Squirrel.Cli;
using Squirrel.Cli.Commands;
using System;
using System.IO;
using System.Reflection;

namespace SyncReleases
{
    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.SetApplicationName("SyncReleases.exe");
                config.SetApplicationVersion(Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0");

                config.AddCommand<SyncCommand>("sync")
                    .WithDescription("Sync releases from GitHub or remote RELEASES folder")
                    .WithExample(new[] { "sync", "--url", "https://github.com/owner/repo", "--release-dir", "./Releases" })
                    .WithExample(new[] { "sync", "--url", "https://github.com/owner/repo", "--token", "ghp_xxx", "--dry-run" })
                    .WithExample(new[] { "sync", "--url", "https://example.com/Releases", "--parallel", "8" });

                config.AddCommand<ValidateCommand>("validate")
                    .WithDescription("Validate a releases directory")
                    .WithExample(new[] { "validate", "--release-dir", "./Releases" })
                    .WithExample(new[] { "validate", "--release-dir", "./Releases", "--fix" });

                config.AddCommand<ListCommand>("list")
                    .WithDescription("List releases in a directory")
                    .WithExample(new[] { "list", "--release-dir", "./Releases" })
                    .WithExample(new[] { "list", "--release-dir", "./Releases", "--output", "json" });

                // Global options are defined in GlobalSettings base class
                config.PropagateExceptions();
                config.ValidateExamples();
            });

            return app.Run(args);
        }
    }
}