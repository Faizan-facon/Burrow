using Spectre.Console;
using System;
using System.Collections.Concurrent;

namespace Squirrel.Cli
{
    public interface IProgressReporter
    {
        ProgressTask AddTask(string description, double maxValue = 100, bool autoStart = true);
        void StartTask(ProgressTask task);
        void StopTask(ProgressTask task);
        void Increment(ProgressTask task, double value = 1);
        void Update(ProgressTask task, double value, string? description = null);
        void Finish(ProgressTask task);
        IDisposable CreateProgressContext(bool autoClear = true);
        IDisposable CreateStatusContext(string status, Spinner? spinner = null);
        void WriteLiveTable(Action<LiveTable> configure);
    }

    public sealed class ProgressReporter : IProgressReporter
    {
        private readonly IAnsiConsole _console;
        private readonly bool _quiet;
        private ProgressContext? _progressContext;
        private StatusContext? _statusContext;
        private readonly ConcurrentDictionary<string, ProgressTask> _tasks = new();

        public ProgressReporter(IAnsiConsole console, bool quiet)
        {
            _console = console;
            _quiet = quiet;
        }

        public ProgressTask AddTask(string description, double maxValue = 100, bool autoStart = true)
        {
            if (_quiet)
            {
                return new SilentProgressTask();
            }

            EnsureProgressContext();
            var task = _progressContext!.AddTask(description, new ProgressTaskSettings
            {
                MaxValue = maxValue,
                AutoStart = autoStart
            });
            return new SpectreProgressTask(task);
        }

        public void StartTask(ProgressTask task)
        {
            if (task is SpectreProgressTask spectreTask)
            {
                spectreTask.Task.StartTask();
            }
        }

        public void StopTask(ProgressTask task)
        {
            if (task is SpectreProgressTask spectreTask)
            {
                spectreTask.Task.StopTask();
            }
        }

        public void Increment(ProgressTask task, double value = 1)
        {
            if (task is SpectreProgressTask spectreTask)
            {
                spectreTask.Task.Increment(value);
            }
        }

        public void Update(ProgressTask task, double value, string? description = null)
        {
            if (task is SpectreProgressTask spectreTask)
            {
                spectreTask.Task.Value = value;
                if (description != null)
                {
                    spectreTask.Task.Description = description;
                }
            }
        }

        public void Finish(ProgressTask task)
        {
            if (task is SpectreProgressTask spectreTask)
            {
                spectreTask.Task.StopTask();
            }
        }

        public IDisposable CreateProgressContext(bool autoClear = true)
        {
            if (_quiet)
            {
                return new SilentProgressContext();
            }

            _console.Progress()
                .AutoClear(autoClear)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn()
                })
                .Start(ctx => { _progressContext = ctx; });

            return new ProgressContextWrapper(this);
        }

        public IDisposable CreateStatusContext(string status, Spinner? spinner = null)
        {
            if (_quiet)
            {
                return new SilentProgressContext();
            }

            spinner ??= Spinner.Known.Dots;

            _console.Status()
                .Spinner(spinner)
                .SpinnerStyle(SquirrelTheme.ProgressBar)
                .Start(status, ctx => { _statusContext = ctx; });

            return new StatusContextWrapper();
        }

        public void WriteLiveTable(Action<LiveTable> configure)
        {
            if (_quiet) return;

            var liveTable = new LiveTable(_console);
            configure(liveTable);
        }

        private void EnsureProgressContext()
        {
            if (_progressContext == null)
            {
                _console.Progress()
                    .AutoClear(true)
                    .Columns(new ProgressColumn[]
                    {
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new RemainingTimeColumn(),
                        new SpinnerColumn()
                    })
                    .Start(ctx => { _progressContext = ctx; });
            }
        }

        private sealed class ProgressContextWrapper : IDisposable
        {
            private readonly ProgressReporter _reporter;

            public ProgressContextWrapper(ProgressReporter reporter)
            {
                _reporter = reporter;
            }

            public void Dispose()
            {
                _reporter._progressContext = null;
            }
        }

        private sealed class StatusContextWrapper : IDisposable
        {
            public void Dispose() { }
        }

        private sealed class SilentProgressContext : IDisposable
        {
            public void Dispose() { }
        }
    }

    public interface ProgressTask
    {
        double Value { set; }
        string Description { set; }
    }

    internal sealed class SpectreProgressTask : ProgressTask
    {
        public Spectre.Console.ProgressTask Task { get; }

        public SpectreProgressTask(Spectre.Console.ProgressTask task)
        {
            Task = task;
        }

        public double Value
        {
            set => Task.Value = value;
        }

        public string Description
        {
            set => Task.Description = value;
        }
    }

    internal sealed class SilentProgressTask : ProgressTask
    {
        public double Value { set { } }
        public string Description { set { } }
    }

    public sealed class LiveTable
    {
        private readonly IAnsiConsole _console;
        private readonly Table _table;
        private int _rowCount = 0;

        public LiveTable(IAnsiConsole console)
        {
            _console = console;
            _table = new Table();
            _table.Border = TableBorder.Rounded;
            _table.BorderStyle = SquirrelTheme.TableBorder;
        }

        public void AddColumn(string header)
        {
            _table.AddColumn(header);
        }

        public void AddRow(params string[] cells)
        {
            _table.AddRow(cells);
            _rowCount++;
        }

        public void UpdateRow(int rowIndex, params string[] cells)
        {
        }

        public void Clear()
        {
            _table.Rows.Clear();
            _rowCount = 0;
        }
    }
}