using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Hackmum.Bethuya.Core.Models;

namespace Bethuya.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class JsonSerializationBenchmark
{
    private Event _event = null!;
    private string _eventJson = null!;
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web);

    [GlobalSetup]
    public void Setup()
    {
        _event = new Event
        {
            Title = "GitHub Copilot Dev Days",
            Description = "A community event for AI-assisted development",
            Capacity = 100,
            CreatedBy = "organizer@hackmum.org",
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddHours(8)
        };

        _eventJson = JsonSerializer.Serialize(_event, s_options);
    }

    [Benchmark]
    public string SerializeEvent()
    {
        return JsonSerializer.Serialize(_event, s_options);
    }

    [Benchmark]
    public Event? DeserializeEvent()
    {
        return JsonSerializer.Deserialize<Event>(_eventJson, s_options);
    }

    [Benchmark]
    public byte[] SerializeEventToUtf8()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_event, s_options);
    }
}
