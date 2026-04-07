using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace SM.Unity;

internal enum RuntimeInstrumentationPolicy
{
    Off = 0,
    SummaryNormal = 1,
    VerboseSmoke = 2,
}

internal static class RuntimeInstrumentation
{
    internal static RuntimeInstrumentationPolicy Policy { get; private set; } = RuntimeInstrumentationPolicy.SummaryNormal;

    internal static bool ShouldEmitSummary => Policy >= RuntimeInstrumentationPolicy.SummaryNormal;
    internal static bool ShouldEmitVerboseArtifacts => Policy >= RuntimeInstrumentationPolicy.VerboseSmoke;

    internal static void SetPolicy(RuntimeInstrumentationPolicy policy)
    {
        Policy = policy;
    }

    internal static TimedFlow BeginFlow(string name) => new(name, ShouldEmitVerboseArtifacts);

    internal static void LogDuration(string name, TimeSpan elapsed, string detail = "")
    {
        if (!ShouldEmitSummary)
        {
            return;
        }

        var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" | {detail}";
        Debug.Log($"[{name}] {elapsed.TotalMilliseconds:0.00} ms{suffix}");
    }

    internal sealed class TimedFlow : IDisposable
    {
        private readonly string _name;
        private readonly bool _verbose;
        private readonly Stopwatch _total = Stopwatch.StartNew();
        private readonly List<string> _steps = new();

        internal TimedFlow(string name, bool verbose)
        {
            _name = name;
            _verbose = verbose;
        }

        internal void Step(string stepName, Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();

            var summary = $"{stepName}={stopwatch.Elapsed.TotalMilliseconds:0.00} ms";
            _steps.Add(summary);
            if (_verbose)
            {
                Debug.Log($"[{_name}] {summary}");
            }
        }

        internal T Step<T>(string stepName, Func<T> action)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = action();
            stopwatch.Stop();

            var summary = $"{stepName}={stopwatch.Elapsed.TotalMilliseconds:0.00} ms";
            _steps.Add(summary);
            if (_verbose)
            {
                Debug.Log($"[{_name}] {summary}");
            }

            return result;
        }

        public void Dispose()
        {
            _total.Stop();
            if (!ShouldEmitSummary)
            {
                return;
            }

            var detail = _steps.Count == 0
                ? string.Empty
                : $" | {string.Join(" | ", _steps)}";
            Debug.Log($"[{_name}] total={_total.Elapsed.TotalMilliseconds:0.00} ms{detail}");
        }
    }
}
