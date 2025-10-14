using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace WarOfMachines.Logging
{
    /// <summary>
    /// Single in-memory log event.
    /// </summary>
    public class LogEvent
    {
        /// <summary>Monotonic sequence number assigned on add.</summary>
        public long Seq { get; internal set; } = 0;

        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
        public LogLevel Level { get; init; }
        public string Category { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string? Exception { get; init; }
        public EventId EventId { get; init; }
        public string? Scope { get; init; }
    }

    /// <summary>
    /// Ring buffer of logs held in memory with simple API.
    /// </summary>
    public static class InMemoryLogStore
    {
        private static readonly ConcurrentQueue<LogEvent> _queue = new();
        private static int _count = 0;
        private static long _seqCounter = 0; // global sequence

        /// <summary>Maximum number of stored entries.</summary>
        public static int Capacity { get; set; } = 2000;

        /// <summary>Total last assigned sequence.</summary>
        public static long LastSeq => Volatile.Read(ref _seqCounter);

        public static void Add(LogEvent evt)
        {
            // Assign sequence first.
            var nextSeq = Interlocked.Increment(ref _seqCounter);
            evt.Seq = nextSeq;

            _queue.Enqueue(evt);
            var current = Interlocked.Increment(ref _count);

            // keep within capacity
            while (current > Capacity && _queue.TryDequeue(out _))
            {
                current = Interlocked.Decrement(ref _count);
            }
        }

        /// <summary>Returns last N entries (newest last).</summary>
        public static List<LogEvent> GetRecent(int take = 200)
        {
            if (take <= 0) take = 1;
            if (take > Capacity) take = Capacity;

            var arr = _queue.ToArray();
            if (arr.Length <= take) return arr.ToList();

            return arr[^take..].ToList();
        }

        /// <summary>
        /// Returns entries with Seq &gt; lastSeenSeq, up to max.
        /// </summary>
        public static List<LogEvent> GetSince(long lastSeenSeq, int max = 200)
        {
            if (max <= 0) max = 1;
            var arr = _queue.ToArray();
            // filter and keep order
            var list = new List<LogEvent>(Math.Min(arr.Length, max));
            foreach (var e in arr)
            {
                if (e.Seq > lastSeenSeq)
                {
                    list.Add(e);
                    if (list.Count >= max) break;
                }
            }
            return list;
        }

        /// <summary>Clears all entries.</summary>
        public static void Clear()
        {
            while (_queue.TryDequeue(out _)) { }
            Interlocked.Exchange(ref _count, 0);
            // keep sequence growing to avoid client confusion
        }
    }

    /// <summary>Provider that writes to InMemoryLogStore.</summary>
    public sealed class InMemoryLoggerProvider : ILoggerProvider, ISUPPORTExternalScope
    {
        private IExternalScopeProvider? _scopeProvider;

        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, () => _scopeProvider);
        public void Dispose() { }
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;
    }

    internal sealed class InMemoryLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly Func<IExternalScopeProvider?> _getScopeProvider;

        public InMemoryLogger(string categoryName, Func<IExternalScopeProvider?> getScopeProvider)
        {
            _categoryName = categoryName;
            _getScopeProvider = getScopeProvider;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            var scopeProvider = _getScopeProvider();
            return scopeProvider?.Push(state) ?? NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string message = formatter(state, exception);

            // Scope text is optional; can be extended later if needed.
            string? scopeText = null;

            var evt = new LogEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = logLevel,
                Category = _categoryName,
                Message = message,
                Exception = exception?.ToString(),
                EventId = eventId,
                Scope = scopeText
            };

            InMemoryLogStore.Add(evt);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    // Interface name fix for older compilers
    public interface ISUPPORTExternalScope : ISupportExternalScope { }
}
