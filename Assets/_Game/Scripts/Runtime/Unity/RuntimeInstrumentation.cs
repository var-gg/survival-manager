using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace SM.Unity;

internal static class RuntimeInstrumentation
{
    internal static TimedFlow BeginFlow(string name) => new(name);

    internal static void LogDuration(string name, TimeSpan elapsed, string detail = "")
    {
        var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail}";
        Debug.Log($"[{name}] {elapsed.TotalMilliseconds:0.00} ms{suffix}");
    }

    internal sealed class TimedFlow : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _total = Stopwatch.StartNew();
        private readonly List<string> _steps = new();

        internal TimedFlow(string name)
        {
            _name = name;
        }

        internal void Step(string stepName, Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();

            var summary = $"{stepName}={stopwatch.Elapsed.TotalMilliseconds:0.00} ms";
            _steps.Add(summary);
            Debug.Log($"[{_name}] {summary}");
        }

        internal T Step<T>(string stepName, Func<T> action)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = action();
            stopwatch.Stop();

            var summary = $"{stepName}={stopwatch.Elapsed.TotalMilliseconds:0.00} ms";
            _steps.Add(summary);
            Debug.Log($"[{_name}] {summary}");
            return result;
        }

        public void Dispose()
        {
            _total.Stop();
            var detail = _steps.Count == 0
                ? string.Empty
                : $" | {string.Join(" | ", _steps)}";
            Debug.Log($"[{_name}] total={_total.Elapsed.TotalMilliseconds:0.00} ms{detail}");
        }
    }
}
