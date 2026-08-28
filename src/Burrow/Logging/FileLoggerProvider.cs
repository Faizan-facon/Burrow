using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using MsLogging = Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Squirrel.SimpleSplat
{
    /// File logger provider for Microsoft.Extensions.Logging
    /// </summary>
    public class FileLoggerProvider : MsLogging.ILoggerProvider
    {
        readonly FileLoggerOptions _options;
        readonly string _filePath;
        readonly object _writeLock = new object();
        StreamWriter _writer;
        bool _disposed;

        public FileLoggerProvider(IOptions<FileLoggerOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _filePath = ResolveFilePath(_options.FilePath);
            InitializeWriter();
        }

        public MsLogging.ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(this, categoryName);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_writeLock)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
        }

        internal void WriteEntry(string categoryName, MsLogging.LogLevel logLevel, EventId eventId, string message, Exception exception)
        {
            if (_disposed) return;

            var builder = new StringBuilder();
            var timestamp = DateTime.Now.ToString("dd/MM/yy HH:mm:ss");
            builder.Append($"[{timestamp}] {logLevel.ToString().ToLower()}: {categoryName}: {message}");

            if (exception != null)
            {
                builder.Append($": {exception}");
            }

            var entry = builder.ToString();

            lock (_writeLock)
            {
                if (_writer != null)
                {
                    _writer.WriteLine(entry);
                    _writer.Flush();
                }
            }
        }

        void InitializeWriter()
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(_filePath, true, Encoding.UTF8, 4096)
            {
                AutoFlush = true
            };
        }

        static string ResolveFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                var exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                var dir = string.IsNullOrEmpty(exePath) ? Environment.CurrentDirectory : Path.GetDirectoryName(exePath);
                return Path.Combine(dir, "Squirrel.log");
            }

            return Path.GetFullPath(filePath);
        }
    }

    /// <summary>
    /// File logger implementation
    /// </summary>
    internal class FileLogger : MsLogging.ILogger
    {
        readonly FileLoggerProvider _provider;
        readonly string _categoryName;

        public FileLogger(FileLoggerProvider provider, string categoryName)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(MsLogging.LogLevel logLevel) => logLevel != MsLogging.LogLevel.None;

        public void Log<TState>(MsLogging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null) return;

            _provider.WriteEntry(_categoryName, logLevel, eventId, message, exception);
        }
    }

    /// <summary>
    /// Options for file logger
    /// </summary>
    public class FileLoggerOptions
    {
        /// <summary>
        /// The file path for the log file. If not specified, defaults to Squirrel.log in the application directory.
        /// </summary>
        public string FilePath { get; set; }
    }

    /// <summary>
    /// Extension methods for adding file logger to ILoggingBuilder
    /// </summary>
    public static class FileLoggerExtensions
    {
        /// <summary>
        /// Adds a file logger to the logging builder
        /// </summary>
        /// <param name="builder">The logging builder</param>
        /// <param name="configure">Optional configuration action</param>
        /// <returns>The logging builder</returns>
        public static MsLogging.ILoggingBuilder AddFile(this MsLogging.ILoggingBuilder builder, Action<FileLoggerOptions> configure = null)
        {
            if (configure != null)
            {
                builder.Services.Configure(configure);
            }

            builder.Services.AddSingleton<MsLogging.ILoggerProvider, FileLoggerProvider>();
            return builder;
        }
    }
}