using BenchmarkDotNet.Attributes;
using Hackmum.Bethuya.Core.Enums;
using Hackmum.Bethuya.Core.Models;

namespace Bethuya.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class RegistrationHotPathBenchmark
{
    private Registration _registration = null!;
    private List<Registration> _registrations = null!;

    [GlobalSetup]
    public void Setup()
    {
        _registration = new Registration
        {
            FullName = "Test User",
            Email = "test@example.com",
            Bio = "A passionate developer",
            Interests = ["AI", "Blazor", ".NET"]
        };

        _registrations = Enumerable.Range(0, 1000)
            .Select(i => new Registration
            {
                FullName = $"User {i}",
                Email = $"user{i}@example.com",
                Interests = ["AI", "Blazor"]
            })
            .ToList();
    }

    [Benchmark]
    public Registration CreateRegistration()
    {
        return new Registration
        {
            FullName = "New User",
            Email = "new@example.com",
            Interests = ["AI"]
        };
    }

    [Benchmark]
    public int FilterPendingRegistrations()
    {
        return _registrations.Count(r => r.Status == RegistrationStatus.Pending);
    }

    [Benchmark]
    public List<Registration> SortByRegistrationDate()
    {
        return [.. _registrations.OrderBy(r => r.RegisteredAt)];
    }
}
