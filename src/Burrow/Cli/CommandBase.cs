using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.IO;
using System.ComponentModel;

namespace Squirrel.Cli
{
    public abstract class CommandBase<TSettings> : Command<TSettings>
        where TSettings : GlobalSettings
    {
        protected CliContext Context { get; private set; } = null!;
        protected IOutputFormatter Output { get; private set; } = null!;
        protected IProgressReporter Progress { get; private set; } = null!;

        public override int Execute(CommandContext context, TSettings settings)
        {
            var console = AnsiConsole.Console;

            Context = new CliContext(
                console,
                CreateLoggerFactory(settings),
                settings,
                settings.Interactive);

            Output = new OutputFormatter(Context.Console, settings.OutputFormat, settings.Quiet);
            Progress = new ProgressReporter(Context.Console, settings.Quiet);

            try
            {
                return ExecuteCommand(settings);
            }
            catch (CliException ex)
            {
                HandleCliException(ex);
                return ex.ExitCode;
            }
            catch (Exception ex)
            {
                HandleUnexpectedException(ex, settings.Verbose);
                return 2;
            }
        }

        protected abstract int ExecuteCommand(TSettings settings);

        protected virtual ILoggerFactory CreateLoggerFactory(TSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.LogFile))
            {
                try
                {
                    var dir = Path.GetDirectoryName(settings.LogFile);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    if (!File.Exists(settings.LogFile))
                    {
                        File.WriteAllText(settings.LogFile, string.Empty);
                    }
                }
                catch { }
            }

            var builder = Microsoft.Extensions.Logging.LoggerFactory.Create(b =>
            {
                if (!settings.Quiet && settings.OutputFormat != OutputFormat.Json)
                {
                    b.AddConsole();
                }
                b.AddDebug();

                b.SetMinimumLevel(settings.Verbose
                    ? Microsoft.Extensions.Logging.LogLevel.Debug
                    : Microsoft.Extensions.Logging.LogLevel.Information);
            });

            Squirrel.SimpleSplat.MicrosoftLogManager.ConfigureMicrosoftLogging(builder);
            return builder;
        }

        protected void HandleCliException(CliException ex)
        {
            if (Context.IsQuiet) return;

            if (Context.OutputFormat == OutputFormat.Json)
            {
                Output.Write(new
                {
                    error = ex.Message,
                    type = ex.GetType().Name,
                    exitCode = ex.ExitCode,
                    suggestion = ex.Suggestion
                });
                return;
            }

            if (Context.OutputFormat == OutputFormat.Text)
            {
                if (ex is ValidationError v && !string.IsNullOrEmpty(v.OptionName))
                {
                    Context.Console.WriteLine($"Validation Error: {v.OptionName} - {ex.Message}");
                }
                else
                {
                    Context.Console.WriteLine($"Error: {ex.Message}");
                }
                if (!string.IsNullOrEmpty(ex.Suggestion))
                {
                    Context.Console.WriteLine($"Example: {ex.Suggestion}");
                }
                return;
            }

            if (ex is ValidationError ve && !string.IsNullOrEmpty(ve.OptionName))
            {
                ErrorPanel.ShowValidationError(Context.Console, ve.OptionName, ex.Message, ve.Suggestion);
            }
            else if (ex is UserError)
            {
                ErrorPanel.Show(Context.Console, "Error", ex.Message, ex.Suggestion);
            }
            else if (ex is SystemError)
            {
                ErrorPanel.ShowException(Context.Console, ex, Context.IsVerbose);
            }
        }

        protected void HandleUnexpectedException(Exception ex, bool verbose)
        {
            if (Context.IsQuiet) return;

            if (Context.OutputFormat == OutputFormat.Json)
            {
                Output.Write(new
                {
                    currentVersion = "",
                    futureVersion = "",
                    releasesToApply = new object[0],
                    error = ex.Message,
                    type = ex.GetType().Name,
                    stackTrace = verbose ? ex.StackTrace : null
                });
                return;
            }

            if (Context.OutputFormat == OutputFormat.Text)
            {
                Context.Console.WriteLine($"Error: {ex.Message}");
                if (verbose && !string.IsNullOrEmpty(ex.StackTrace))
                {
                    Context.Console.WriteLine(ex.StackTrace);
                }
                return;
            }

            if (Context.OutputFormat == OutputFormat.Table)
            {
                var table = new Spectre.Console.Table();
                table.Border = Spectre.Console.TableBorder.Square;
                table.AddColumn("Error");
                table.AddColumn("Type");
                table.AddRow(Markup.Escape(ex.Message), Markup.Escape(ex.GetType().Name));
                Context.Console.Write(table);
                return;
            }

            ErrorPanel.ShowException(Context.Console, ex, verbose);
        }

        protected void ValidateRequired(string value, string optionName, string example = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                var argName = optionName.TrimStart('-', '<', '>').ToUpperInvariant();
                throw new ValidationError(
                    $"Missing required argument: {argName}",
                    optionName,
                    example != null ? $"Example: {example}" : $"Provide a value for {optionName}");
            }
        }

        protected void ValidatePathExists(string path, string optionName, bool isDirectory = false)
        {
            if (isDirectory)
            {
                if (!Directory.Exists(path))
                {
                    throw new ValidationError(
                        $"Directory not found: {path}",
                        optionName,
                        $"Ensure the directory exists or create it first");
                }
            }
            else
            {
                if (!File.Exists(path))
                {
                    throw new ValidationError(
                        $"File not found: {path}",
                        optionName,
                        $"Ensure the file exists");
                }
            }
        }

        protected void ShowDeprecationWarning(string oldSyntax, string newSyntax)
        {
            if (!Context.IsQuiet)
            {
                Context.Console.MarkupLine($"[{SquirrelTheme.Warning}]⚠ Deprecation Warning:[/] '{oldSyntax.EscapeMarkup()}' is deprecated. Use '{newSyntax.EscapeMarkup()}' instead.");
            }
        }
    }
}