using Spectre.Console;
using System;
using System.Collections.Generic;

namespace Squirrel.Cli
{
    public static class CliPrompts
    {
        public static bool Confirm(IAnsiConsole console, string message, bool defaultValue = true)
        {
            return console.Prompt(
                new Spectre.Console.ConfirmationPrompt(message)
                {
                    DefaultValue = defaultValue
                });
        }

        public static string PromptText(IAnsiConsole console, string message, string? defaultValue = null, bool allowEmpty = false)
        {
            var prompt = new Spectre.Console.TextPrompt<string>(message)
            {
                AllowEmpty = allowEmpty
            };

            if (defaultValue != null)
            {
                prompt.DefaultValue(defaultValue);
            }

            return console.Prompt(prompt);
        }

        public static string PromptSecret(IAnsiConsole console, string message, char mask = '*')
        {
            return console.Prompt(
                new Spectre.Console.TextPrompt<string>(message)
                {
                    IsSecret = true
                });
        }

        public static T PromptSelection<T>(IAnsiConsole console, string message, T[] choices, Func<T, string> display)
            where T : notnull
        {
            var prompt = new Spectre.Console.SelectionPrompt<T>
            {
                Title = message,
                Converter = display
            };

            prompt.AddChoices(choices);

            return console.Prompt(prompt);
        }

        public static string[] PromptMultiSelect(IAnsiConsole console, string message, string[] choices)
        {
            var prompt = new Spectre.Console.MultiSelectionPrompt<string>
            {
                Title = message
            };

            prompt.AddChoices(choices);

            return console.Prompt(prompt).ToArray();
        }
    }
}