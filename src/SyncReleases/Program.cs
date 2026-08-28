using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mono.Options;
using Octokit;
using Squirrel.SimpleSplat;
using Squirrel;
using Squirrel.Json;

namespace SyncReleases
{
    class Program : IEnableLogger 
    {
        static OptionSet opts;
        public static int Main(string[] args)
        {
            // Configure Microsoft.Extensions.Logging with Console, Debug, and File sinks
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.AddFile(options =>
                {
                    options.FilePath = GetLogFilePath("SyncReleases");
                });
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            });
            MicrosoftLogManager.ConfigureMicrosoftLogging(loggerFactory);

            var pg = new Program();
            try {
                return pg.main(args).Result;
            } catch (Exception ex) {
                // NB: Normally this is a terrible idea but we want to make
                // sure Setup.exe above us gets the nonzero error code
                Console.Error.WriteLine(ex);
                return -1;
            }
        }

        static string GetLogFilePath(string commandSuffix)
        {
            var exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            var dir = string.IsNullOrEmpty(exePath) ? Environment.CurrentDirectory : Path.GetDirectoryName(exePath);
            return Path.Combine(dir, $"Squirrel-{commandSuffix}.log");
        }

        async Task<int> main(string[] args)
        {
            var releaseDir = default(string);
            var repoUrl = default(string);
            var token = default(string);

            opts = new OptionSet() {
                "Usage: SyncReleases.exe command [OPTS]",
                "Builds a Releases directory from releases on GitHub",
                "",
                "Options:",
                { "h|?|help", "Display Help and exit", _ => {} },
                { "r=|releaseDir=", "Path to a release directory to download to", v => releaseDir = v},
                { "u=|url=", "When pointing to GitHub, use the URL to the repository root page, else point to an existing remote Releases folder", v => repoUrl = v},
                { "t=|token=", "The OAuth token to use as login credentials", v => token = v},
            };

            opts.Parse(args);

            if (repoUrl == null || repoUrl.StartsWith("http", true, CultureInfo.InvariantCulture) == false) {
                this.Log().Error("Invalid repository URL");
                ShowHelp();
                return -1;
            }

            var releaseDirectoryInfo = new DirectoryInfo(releaseDir ?? Path.Combine(".", "Releases"));
            if (!releaseDirectoryInfo.Exists) releaseDirectoryInfo.Create();

            var githubException = default(Exception);
            try {
                await SyncImplementations.SyncFromGitHub(repoUrl, token, releaseDirectoryInfo);
                return 0;
            } catch (Exception ex) {
                githubException = ex;
                this.Log().Warn("Attempting to sync URL as remote RELEASES folder: {0}", ex.Message);
            }

            try {
                await SyncImplementations.SyncRemoteReleases(new Uri(repoUrl), releaseDirectoryInfo);
            } catch (Exception) {
                this.Log().Error("Failed to sync URL as GitHub repo: {0}", githubException.Message);
                throw;
            }

            return 0;
        }
        
        public void ShowHelp()
        {
            opts.WriteOptionDescriptions(Console.Out);
        }
    }

}
