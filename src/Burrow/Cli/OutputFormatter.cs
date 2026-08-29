using Spectre.Console;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Squirrel.Cli
{
    public interface IOutputFormatter
    {
        void Write<T>(T data);
        void WriteLine(string text);
        void WriteMarkup(string markup);
    }

    public sealed class OutputFormatter : IOutputFormatter
    {
        private readonly IAnsiConsole _console;
        private readonly OutputFormat _format;
        private readonly bool _quiet;
        private readonly JsonSerializerOptions _jsonOptions;

        public OutputFormatter(IAnsiConsole console, OutputFormat format, bool quiet)
        {
            _console = console;
            _format = format;
            _quiet = quiet;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        public void Write<T>(T data)
        {
            if (_quiet) return;

            switch (_format)
            {
                case OutputFormat.Json:
                    var json = JsonSerializer.Serialize(data, _jsonOptions);
                    _console.WriteLine(json);
                    break;

                case OutputFormat.Table:
                    WriteAsTable(data);
                    break;

                case OutputFormat.Text:
                default:
                    WriteAsText(data);
                    break;
            }
        }

        public void WriteLine(string text)
        {
            if (_quiet) return;
            _console.WriteLine(text);
        }

        public void WriteMarkup(string markup)
        {
            if (_quiet) return;
            _console.MarkupLine(markup);
        }

        private void WriteAsText<T>(T data)
        {
            if (data is string s)
            {
                _console.WriteLine(s);
            }
            else if (data is System.Collections.IEnumerable enumerable && data is not string)
            {
                foreach (var item in enumerable)
                {
                    _console.WriteLine(item?.ToString() ?? "");
                }
            }
            else
            {
                _console.WriteLine(data?.ToString() ?? "");
            }
        }

        private void WriteAsTable<T>(T data)
        {
            if (data == null) return;

            if (data is System.Collections.IEnumerable enumerable && data is not string)
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    if (item != null) list.Add(item);
                }
                WriteTable(list);
            }
            else
            {
                WriteTable(new List<object> { data });
            }
        }

        private void WriteTable(List<object> items)
        {
            if (items.Count == 0) return;

            var first = items[0];
            var props = first.GetType().GetProperties();

            var table = new Table();
            table.Border = TableBorder.Square;
            table.BorderStyle = SquirrelTheme.TableBorder;

            foreach (var prop in props)
            {
                table.AddColumn(prop.Name);
            }

            foreach (var item in items)
            {
                var row = new List<string>();
                foreach (var prop in props)
                {
                    var value = prop.GetValue(item);
                    row.Add(value?.ToString() ?? "");
                }
                table.AddRow(row.ToArray());
            }

            _console.Write(table);
        }
    }
}