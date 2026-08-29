using Spectre.Console.Cli;
using System.ComponentModel;

namespace Squirrel.Cli
{
    public enum OutputFormat
    {
        Text,
        Json,
        Table
    }

    public class GlobalSettings : CommandSettings
    {
        [CommandOption("-v|--verbose")]
        [Description("Enable verbose logging")]
        public bool Verbose { get; set; }

        [CommandOption("-q|--quiet")]
        [Description("Suppress non-error output")]
        public bool Quiet { get; set; }

        [CommandOption("--log-file")]
        [Description("Write logs to file")]
        public string? LogFile { get; set; }

        [CommandOption("--no-color")]
        [Description("Disable colored output")]
        public bool NoColor { get; set; }

        [CommandOption("--output")]
        [Description("Output format: text|json|table (default: text)")]
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Text;

        [CommandOption("--interactive")]
        [Description("Enable interactive mode with command picker")]
        public bool Interactive { get; set; }
    }
}