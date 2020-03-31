// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;

namespace Microsoft.Tye.Hosting
{
    internal class ServiceLoggerProvider : ILoggerProvider
    {
        private readonly Subject<string> _logs;

        public ServiceLoggerProvider(Subject<string> logs)
        {
            _logs = logs;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ServiceLogger(categoryName, _logs);
        }

        public void Dispose()
        {
        }

        private class ServiceLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly Subject<string> _logs;

            public ServiceLogger(string categoryName, Subject<string> logs)
            {
                _categoryName = categoryName;
                _logs = logs;
            }

            public IDisposable? BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _logs.OnNext($"[{logLevel}]: {formatter(state, exception)}");

                if (exception != null)
                {
                    _logs.OnNext(exception.ToString());
                }
            }
        }
    }
}
