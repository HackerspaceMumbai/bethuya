using BenchmarkDotNet.Attributes;
using Hackmum.Bethuya.Core.Models;

namespace Bethuya.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class EventIdBenchmark
{
    [Benchmark(Baseline = true)]
    public Guid CreateEventId()
    {
        return Guid.CreateVersion7();
    }

    [Benchmark]
    public Guid CreateEventIdViaNewGuid()
    {
        return Guid.NewGuid();
    }

    [Benchmark]
    public Event CreateEvent()
    {
        return new Event
        {
            Title = "GitHub Copilot Dev Days: Mumbai",
            CreatedBy = "organizer@hackmum.org"
        };
    }
}
