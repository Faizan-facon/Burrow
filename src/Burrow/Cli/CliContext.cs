using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using System;

namespace Squirrel.Cli
{
    public sealed class CliContext
    {
        public IAnsiConsole Console { get; }
        public ILoggerFactory LoggerFactory { get; }
        public ILogger Logger { get; }
        public GlobalSettings GlobalSettings { get; }
        public bool IsInteractive { get; }
        public bool IsQuiet => GlobalSettings.Quiet;
        public bool IsVerbose => GlobalSettings.Verbose;
        public bool NoColor => GlobalSettings.NoColor;
        public OutputFormat OutputFormat => GlobalSettings.OutputFormat;

        public CliContext(
            IAnsiConsole console,
            ILoggerFactory loggerFactory,
            GlobalSettings globalSettings,
            bool isInteractive = false)
        {
            Console = console;
            LoggerFactory = loggerFactory;
            GlobalSettings = globalSettings;
            IsInteractive = isInteractive;

            var logger = loggerFactory.CreateLogger("Squirrel.Cli");
            Logger = logger;

            console.Profile.Capabilities.ColorSystem = NoColor
                ? ColorSystem.NoColors
                : console.Profile.Capabilities.ColorSystem;
        }

        public SimpleLogger Log() => new SimpleLogger(Logger, this);

        public SimpleLogger SimpleLog => new SimpleLogger(Logger, this);

        public TLogger CreateLogger<TLogger>() where TLogger : class
        {
            return LoggerFactory.CreateLogger<TLogger>() as TLogger;
        }

        public void WriteError(string message)
        {
            if (!IsQuiet)
            {
                Console.MarkupLine($"[{SquirrelTheme.Error}]✗ Error[/]: {message.EscapeMarkup()}");
            }
        }

        public void WriteSuccess(string message)
        {
            if (!IsQuiet)
            {
                Console.MarkupLine($"[{SquirrelTheme.Success}]✓[/] {message.EscapeMarkup()}");
            }
        }

        public void WriteWarning(string message)
        {
            if (!IsQuiet)
            {
                Console.MarkupLine($"[{SquirrelTheme.Warning}]⚠ Warning[/]: {message.EscapeMarkup()}");
            }
        }

        public void WriteInfo(string message)
        {
            if (!IsQuiet)
            {
                Console.MarkupLine($"[{SquirrelTheme.Info}]ℹ[/] {message.EscapeMarkup()}");
            }
        }

        public void WriteVerbose(string message)
        {
            if (IsVerbose && !IsQuiet)
            {
                Console.MarkupLine($"[{SquirrelTheme.Dim}]{message.EscapeMarkup()}[/]");
            }
        }

        public class SimpleLogger
        {
            private readonly ILogger _logger;
            private readonly CliContext _context;

            public SimpleLogger(ILogger logger, CliContext context)
            {
                _logger = logger;
                _context = context;
            }

            public void Info(string message, params object[] args)
            {
                if (!_context.IsQuiet)
                    _logger.LogInformation(message, args);
            }

            public void Warn(string message, params object[] args)
            {
                if (!_context.IsQuiet)
                    _logger.LogWarning(message, args);
            }

            public void Error(string message, params object[] args)
            {
                _logger.LogError(message, args);
            }

            public void ErrorException(string message, Exception ex)
            {
                _logger.LogError(ex, message);
            }

            public void WarnException(string message, Exception ex)
            {
                _logger.LogWarning(ex, message);
            }

            public void Fatal(string message, params object[] args)
            {
                _logger.LogCritical(message, args);
            }
        }
    }
}