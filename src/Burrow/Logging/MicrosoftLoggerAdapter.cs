using System;
using System.Globalization;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using MsLogging = Microsoft.Extensions.Logging;
using Squirrel.SimpleSplat;

namespace Squirrel.SimpleSplat
{
#if !PORTABLE && !WINDOWS_PHONE && !NETFX_CORE
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public sealed class LocalizableAttribute : Attribute
    {
        public LocalizableAttribute(bool isLocalizable) { }
    }
#endif

    /// <summary>
    /// Wraps Microsoft.Extensions.Logging.ILogger to implement Squirrel.SimpleSplat.ILogger
    /// </summary>
    internal class MicrosoftLogger : ILogger
    {
        readonly MsLogging.ILogger _inner;
        readonly LogLevel _minLevel;

        public MicrosoftLogger(MsLogging.ILogger inner, LogLevel minLevel = LogLevel.Debug)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _minLevel = minLevel;
        }

        public void Write([Localizable(false)] string message, LogLevel logLevel)
        {
            if ((int)logLevel < (int)_minLevel) return;

            var msLevel = MapLogLevel(logLevel);
            _inner.Log(msLevel, new EventId(0), message, (Exception)null, (state, ex) => state);
        }

        public LogLevel Level { get; set; } = LogLevel.Debug;

        static MsLogging.LogLevel MapLogLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug: return MsLogging.LogLevel.Debug;
                case LogLevel.Info: return MsLogging.LogLevel.Information;
                case LogLevel.Warn: return MsLogging.LogLevel.Warning;
                case LogLevel.Error: return MsLogging.LogLevel.Error;
                case LogLevel.Fatal: return MsLogging.LogLevel.Critical;
                default: return MsLogging.LogLevel.Information;
            }
        }
    }

    /// <summary>
    /// Implements IFullLogger by formatting messages and delegating to MicrosoftLogger.Write()
    /// </summary>
    internal class MicrosoftFullLogger : IFullLogger
    {
        readonly MicrosoftLogger _inner;
        readonly string _prefix;
        readonly MethodInfo _stringFormat;

        public MicrosoftFullLogger(MicrosoftLogger inner, Type callingType)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _prefix = String.Format(CultureInfo.InvariantCulture, "{0}: ", callingType.Name);

            _stringFormat = typeof(string).GetMethod("Format", new[] { typeof(IFormatProvider), typeof(string), typeof(object[]) });
            Contract.Requires(_stringFormat != null);
        }

        string InvokeStringFormat(IFormatProvider formatProvider, string message, object[] args)
        {
            var sfArgs = new object[3];
            sfArgs[0] = formatProvider;
            sfArgs[1] = message;
            sfArgs[2] = args;
            return (string)_stringFormat.Invoke(null, sfArgs);
        }

        public void Debug<T>(T value) => _inner.Write(_prefix + value, LogLevel.Debug);
        public void Debug<T>(IFormatProvider formatProvider, T value) => _inner.Write(String.Format(formatProvider, "{0}{1}", _prefix, value), LogLevel.Debug);
        public void DebugException([Localizable(false)] string message, Exception exception) => _inner.Write(String.Format("{0}{1}: {2}", _prefix, message, exception), LogLevel.Debug);
        public void Debug(IFormatProvider formatProvider, [Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(formatProvider, message, args), LogLevel.Debug);
        public void Debug([Localizable(false)] string message) => _inner.Write(_prefix + message, LogLevel.Debug);
        public void Debug([Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(CultureInfo.InvariantCulture, message, args), LogLevel.Debug);
        public void Debug<TArgument>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(formatProvider, message, argument), LogLevel.Debug);
        public void Debug<TArgument>([Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Debug);
        public void Debug<TArgument1, TArgument2>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Debug);
        public void Debug<TArgument1, TArgument2>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Debug);
        public void Debug<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Debug);
        public void Debug<TArgument1, TArgument2, TArgument3>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Debug);

        public void Info<T>(T value) => _inner.Write(_prefix + value, LogLevel.Info);
        public void Info<T>(IFormatProvider formatProvider, T value) => _inner.Write(String.Format(formatProvider, "{0}{1}", _prefix, value), LogLevel.Info);
        public void InfoException([Localizable(false)] string message, Exception exception) => _inner.Write(String.Format("{0}{1}: {2}", _prefix, message, exception), LogLevel.Info);
        public void Info(IFormatProvider formatProvider, [Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(formatProvider, message, args), LogLevel.Info);
        public void Info([Localizable(false)] string message) => _inner.Write(_prefix + message, LogLevel.Info);
        public void Info([Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(CultureInfo.InvariantCulture, message, args), LogLevel.Info);
        public void Info<TArgument>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(formatProvider, message, argument), LogLevel.Info);
        public void Info<TArgument>([Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Info);
        public void Info<TArgument1, TArgument2>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Info);
        public void Info<TArgument1, TArgument2>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Info);
        public void Info<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Info);
        public void Info<TArgument1, TArgument2, TArgument3>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Info);

        public void Warn<T>(T value) => _inner.Write(_prefix + value, LogLevel.Warn);
        public void Warn<T>(IFormatProvider formatProvider, T value) => _inner.Write(String.Format(formatProvider, "{0}{1}", _prefix, value), LogLevel.Warn);
        public void WarnException([Localizable(false)] string message, Exception exception) => _inner.Write(String.Format("{0}{1}: {2}", _prefix, message, exception), LogLevel.Warn);
        public void Warn(IFormatProvider formatProvider, [Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(formatProvider, message, args), LogLevel.Warn);
        public void Warn([Localizable(false)] string message) => _inner.Write(_prefix + message, LogLevel.Warn);
        public void Warn([Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(CultureInfo.InvariantCulture, message, args), LogLevel.Warn);
        public void Warn<TArgument>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(formatProvider, message, argument), LogLevel.Warn);
        public void Warn<TArgument>([Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Warn);
        public void Warn<TArgument1, TArgument2>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Warn);
        public void Warn<TArgument1, TArgument2>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Warn);
        public void Warn<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Warn);
        public void Warn<TArgument1, TArgument2, TArgument3>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Warn);

        public void Error<T>(T value) => _inner.Write(_prefix + value, LogLevel.Error);
        public void Error<T>(IFormatProvider formatProvider, T value) => _inner.Write(String.Format(formatProvider, "{0}{1}", _prefix, value), LogLevel.Error);
        public void ErrorException([Localizable(false)] string message, Exception exception) => _inner.Write(String.Format("{0}{1}: {2}", _prefix, message, exception), LogLevel.Error);
        public void Error(IFormatProvider formatProvider, [Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(formatProvider, message, args), LogLevel.Error);
        public void Error([Localizable(false)] string message) => _inner.Write(_prefix + message, LogLevel.Error);
        public void Error([Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(CultureInfo.InvariantCulture, message, args), LogLevel.Error);
        public void Error<TArgument>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(formatProvider, message, argument), LogLevel.Error);
        public void Error<TArgument>([Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Error);
        public void Error<TArgument1, TArgument2>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Error);
        public void Error<TArgument1, TArgument2>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Error);
        public void Error<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Error);
        public void Error<TArgument1, TArgument2, TArgument3>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Error);

        public void Fatal<T>(T value) => _inner.Write(_prefix + value, LogLevel.Fatal);
        public void Fatal<T>(IFormatProvider formatProvider, T value) => _inner.Write(String.Format(formatProvider, "{0}{1}", _prefix, value), LogLevel.Fatal);
        public void FatalException([Localizable(false)] string message, Exception exception) => _inner.Write(String.Format("{0}{1}: {2}", _prefix, message, exception), LogLevel.Fatal);
        public void Fatal(IFormatProvider formatProvider, [Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(formatProvider, message, args), LogLevel.Fatal);
        public void Fatal([Localizable(false)] string message) => _inner.Write(_prefix + message, LogLevel.Fatal);
        public void Fatal([Localizable(false)] string message, params object[] args) => _inner.Write(_prefix + InvokeStringFormat(CultureInfo.InvariantCulture, message, args), LogLevel.Fatal);
        public void Fatal<TArgument>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(formatProvider, message, argument), LogLevel.Fatal);
        public void Fatal<TArgument>([Localizable(false)] string message, TArgument argument) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Fatal);
        public void Fatal<TArgument1, TArgument2>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Fatal);
        public void Fatal<TArgument1, TArgument2>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Fatal);
        public void Fatal<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, [Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Fatal);
        public void Fatal<TArgument1, TArgument2, TArgument3>([Localizable(false)] string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3) => _inner.Write(_prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Fatal);

        public void Write([Localizable(false)] string message, LogLevel logLevel) => _inner.Write(message, logLevel);

        public LogLevel Level
        {
            get => _inner.Level;
            set => _inner.Level = value;
        }
    }

    /// <summary>
    /// ILogManager implementation that creates loggers using Microsoft.Extensions.Logging.ILoggerFactory
    /// </summary>
    public class MicrosoftLogManager : ILogManager
    {
        static MicrosoftLogManager _instance;
        static readonly object _instanceLock = new object();
        static ILoggerFactory _factory;

        /// <summary>
        /// Gets the singleton instance of MicrosoftLogManager. Returns null if not configured.
        /// </summary>
        public static MicrosoftLogManager Instance
        {
            get { lock (_instanceLock) return _instance; }
        }

        /// <summary>
        /// Configures Microsoft logging with the provided ILoggerFactory.
        /// Call this at application startup before any logging occurs.
        /// </summary>
        /// <param name="factory">The ILoggerFactory to use for creating loggers.</param>
        public static void ConfigureMicrosoftLogging(ILoggerFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            lock (_instanceLock)
            {
                _factory = factory;
                _instance = new MicrosoftLogManager(factory);
            }
        }

        /// <summary>
        /// Creates a basic ILogger using the configured factory, or null if not configured.
        /// Used as fallback for ILogger registrations.
        /// </summary>
        public static ILogger CreateLogger()
        {
            lock (_instanceLock)
            {
                if (_factory == null) return null;
                var logger = _factory.CreateLogger("Squirrel");
                return new MicrosoftLogger((MsLogging.ILogger)logger);
            }
        }

        /// <summary>
        /// Resets the MicrosoftLogManager to unconfigured state (uses DebugLogger fallback).
        /// </summary>
        public static void Reset()
        {
            lock (_instanceLock)
            {
                _factory = null;
                _instance = null;
            }
        }

        readonly ILoggerFactory _loggerFactory;
        readonly MemoizingMRUCache<Type, IFullLogger> _loggerCache;

        MicrosoftLogManager(ILoggerFactory factory)
        {
            _loggerFactory = factory ?? throw new ArgumentNullException(nameof(factory));

            _loggerCache = new MemoizingMRUCache<Type, IFullLogger>((type, _) =>
            {
                var msLogger = _loggerFactory.CreateLogger(type.FullName ?? type.Name);
                var wrapper = new MicrosoftLogger((MsLogging.ILogger)msLogger);
                return new MicrosoftFullLogger(wrapper, type);
            }, 64);
        }

        public IFullLogger GetLogger(Type type)
        {
            if (LogHost.suppressLogging) return LogHost.nullLogger;
            if (type == typeof(MemoizingMRUCache<Type, IFullLogger>)) return LogHost.nullLogger;

            lock (_loggerCache)
            {
                return _loggerCache.Get(type);
            }
        }
    }
}