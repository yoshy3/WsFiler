using System;
using System.Diagnostics;

namespace WsFiler.App;

internal sealed class ThrottledProgress<T> : IProgress<T>
{
    private readonly IProgress<T> inner;
    private readonly long minimumTicks;
    private readonly object sync = new();
    private long lastReportTimestamp;
    private T? latestValue;
    private bool hasLatestValue;

    public ThrottledProgress(IProgress<T> inner, TimeSpan minimumInterval)
    {
        this.inner = inner;
        minimumTicks = (long)(minimumInterval.TotalSeconds * Stopwatch.Frequency);
    }

    public void Report(T value)
    {
        var shouldReport = false;
        lock (sync)
        {
            latestValue = value;
            hasLatestValue = true;

            var now = Stopwatch.GetTimestamp();
            if (lastReportTimestamp == 0 ||
                now - lastReportTimestamp >= minimumTicks)
            {
                lastReportTimestamp = now;
                shouldReport = true;
            }
        }

        if (shouldReport)
        {
            inner.Report(value);
        }
    }

    public void Flush()
    {
        T value;
        lock (sync)
        {
            if (!hasLatestValue)
            {
                return;
            }

            value = latestValue!;
            hasLatestValue = false;
            lastReportTimestamp = Stopwatch.GetTimestamp();
        }

        inner.Report(value);
    }
}
