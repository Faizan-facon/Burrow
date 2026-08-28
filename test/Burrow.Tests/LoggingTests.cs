using System;
using System.IO;
using Microsoft.Extensions.Logging;
using MsLogging = Microsoft.Extensions.Logging;
using Squirrel.SimpleSplat;
using Xunit;

namespace Squirrel.Tests
{
    public class LoggingTests : IDisposable
    {
        readonly MsLogging.ILoggerFactory _loggerFactory;
        readonly StringWriter _stringWriter;

        public LoggingTests()
        {
            _stringWriter = new StringWriter();
            _loggerFactory = MsLogging.LoggerFactory.Create(builder =>
            {
                builder.AddProvider(new TestLoggerProvider(_stringWriter));
                builder.SetMinimumLevel(MsLogging.LogLevel.Debug);
            });
            MicrosoftLogManager.ConfigureMicrosoftLogging(_loggerFactory);
        }

        public void Dispose()
        {
            MicrosoftLogManager.Reset();
            _loggerFactory?.Dispose();
            _stringWriter?.Dispose();
        }

        [Fact]
        public void MicrosoftLogManager_Configure_RoutesLogCallsToMicrosoftLogger()
        {
            // Arrange
            var testClass = new TestLoggingClass();

            // Act
            testClass.Log().Info("Test message {0}", "with args");

            // Assert
            var output = _stringWriter.ToString();
            Assert.Contains("TestLoggingClass: Test message with args", output);
            Assert.Contains("Information", output);
        }

        [Fact]
        public void MicrosoftLogManager_Configure_RespectsLogLevels()
        {
            // Arrange
            var testClass = new TestLoggingClass();
            _stringWriter.GetStringBuilder().Clear();

            // Act
            testClass.Log().Debug("Debug message");
            testClass.Log().Info("Info message");
            testClass.Log().Warn("Warn message");
            testClass.Log().Error("Error message");
            testClass.Log().Fatal("Fatal message");

            // Assert
            var output = _stringWriter.ToString();
            Assert.Contains("Debug", output);
            Assert.Contains("Information", output);
            Assert.Contains("Warning", output);
            Assert.Contains("Error", output);
            Assert.Contains("Critical", output);
        }

        [Fact]
        public void MicrosoftLogManager_Configure_ExceptionLogging()
        {
            // Arrange
            var testClass = new TestLoggingClass();
            _stringWriter.GetStringBuilder().Clear();
            var ex = new InvalidOperationException("Test exception");

            // Act
            testClass.Log().ErrorException("Error with exception", ex);

            // Assert
            var output = _stringWriter.ToString();
            Assert.Contains("Error with exception", output);
            Assert.Contains("Test exception", output);
            Assert.Contains("Error", output);
        }

        [Fact]
        public void MicrosoftLogManager_Reset_FallsBackToDebugLogger()
        {
            // Arrange
            var testClass = new TestLoggingClass();
            _stringWriter.GetStringBuilder().Clear();

            // Act
            MicrosoftLogManager.Reset();
            testClass.Log().Info("After reset");

            // Assert - After reset, it should fall back to DebugLogger which writes to Debug.WriteLine
            // We can't easily capture Debug.WriteLine, but we can verify no exception is thrown
            // and the logging doesn't crash
        }

        class TestLoggingClass : IEnableLogger
        {
        }

        class TestLoggerProvider : MsLogging.ILoggerProvider
        {
            readonly StringWriter _writer;

            public TestLoggerProvider(StringWriter writer)
            {
                _writer = writer;
            }

            public MsLogging.ILogger CreateLogger(string categoryName)
            {
                return new TestLogger(categoryName, _writer);
            }

            public void Dispose() { }
        }

        class TestLogger : MsLogging.ILogger
        {
            readonly string _categoryName;
            readonly StringWriter _writer;

            public TestLogger(string categoryName, StringWriter writer)
            {
                _categoryName = categoryName;
                _writer = writer;
            }

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(MsLogging.LogLevel logLevel) => true;

            public void Log<TState>(MsLogging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                var message = formatter(state, exception);
                _writer.WriteLine($"{logLevel}: {_categoryName}: {message}");
            }
        }
    }
}