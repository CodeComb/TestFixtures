﻿using System;
using Microsoft.Extensions.Logging;

namespace CodeComb.Testing
{
    public class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        public IDisposable BeginScopeImpl(object state)
        {
            return NullDisposable.Instance;
        }

        public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        private class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new NullDisposable();

            public void Dispose()
            {
                // intentionally does nothing
            }
        }
    }
}
