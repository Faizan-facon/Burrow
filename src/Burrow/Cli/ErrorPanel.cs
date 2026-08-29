using Spectre.Console;
using System;

namespace Squirrel.Cli
{
    public abstract class CliException : Exception
    {
        public int ExitCode { get; }
        public string? Suggestion { get; }

        protected CliException(string message, int exitCode, string? suggestion = null)
            : base(message)
        {
            ExitCode = exitCode;
            Suggestion = suggestion;
        }
    }

    public sealed class UserError : CliException
    {
        public UserError(string message, string? suggestion = null)
            : base(message, 1, suggestion) { }
    }

    public sealed class SystemError : CliException
    {
        public SystemError(string message, string? suggestion = null)
            : base(message, 2, suggestion) { }

        public SystemError(string message, Exception innerException, string? suggestion = null)
            : base(message, 2, suggestion)
        {
        }
    }

    public sealed class ValidationError : CliException
    {
        public string? OptionName { get; }

        public ValidationError(string message, string? optionName = null, string? suggestion = null)
            : base(message, 3, suggestion)
        {
            OptionName = optionName;
        }
    }

    public static class ErrorPanel
    {
        public static void Show(IAnsiConsole console, string title, string message, string? suggestion = null, string? example = null)
        {
            var panel = new Panel(message.EscapeMarkup())
            {
                Header = new PanelHeader($"[red]✗ {title.EscapeMarkup()}[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("red"),
                Padding = new Padding(1, 1, 1, 1)
            };

            console.Write(panel);

            if (!string.IsNullOrEmpty(suggestion))
            {
                console.MarkupLine($"[blue]💡 {suggestion.EscapeMarkup()}[/]");
            }

            if (!string.IsNullOrEmpty(example))
            {
                console.MarkupLine($"[grey]Example:[/] [white]{example.EscapeMarkup()}[/]");
            }
        }

        public static void ShowValidationError(IAnsiConsole console, string optionName, string message, string? example = null)
        {
            var panel = new Panel(
                $"[red]{optionName.EscapeMarkup()}[/] {message.EscapeMarkup()}")
            {
                Header = new PanelHeader($"[red]✗ Validation Error[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("red"),
                Padding = new Padding(1, 1, 1, 1)
            };

            console.Write(panel);

            if (!string.IsNullOrEmpty(example))
            {
                console.MarkupLine($"[grey]Example:[/] [white]{example.EscapeMarkup()}[/]");
            }
        }

        public static void ShowException(IAnsiConsole console, Exception ex, bool verbose)
        {
            var panel = new Panel(
                $"[red]{ex.Message.EscapeMarkup()}[/]")
            {
                Header = new PanelHeader($"[red]✗ System Error[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("red"),
                Padding = new Padding(1, 1, 1, 1)
            };

            console.Write(panel);

            if (verbose)
            {
                console.WriteException(ex, ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks);
            }
        }

        public static void ShowDidYouMean(IAnsiConsole console, string input, string[] suggestions)
        {
            if (suggestions.Length == 0) return;

            console.MarkupLine($"[yellow]Unknown command or option:[/] [white]{input.EscapeMarkup()}[/]");
            console.Markup($"[blue]Did you mean:[/] ");

            for (int i = 0; i < suggestions.Length; i++)
            {
                if (i > 0) console.Markup(", ");
                console.Markup($"[cyan]{suggestions[i].EscapeMarkup()}[/]");
            }
            console.WriteLine();
        }
    }
}