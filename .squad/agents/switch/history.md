# Project Context

- **Owner:** Augustine Correa
- **Project:** AI-augmented, agent-first community event platform built for HackerspaceMumbai
- **Stack:** .NET 10, C# 14, Aspire 13, Blazor Web App, .NET MAUI Blazor Hybrid, Blazor Blueprint UI, TUnit, Playwright
- **Created:** 2026-03-21T11:50:19.882Z

## Learnings

- Bethuya follows test-first intent, uses TUnit rather than xUnit/NUnit, and expects visual proof for UI changes before completion.
- E2E tests use Playwright with MSTest (not TUnit) and MUST use `data-test` selectors for stability.
- Key test selector patterns: `[data-test='new-event-btn']`, `[data-test='create-event-submit']`, `[data-test='event-card']`, `[data-test='notification']`.
- Test projects reference multiple dependencies: Core, Backend (for Contracts), Agents, Shared, ServiceDefaults.
- TUnit assertions use `await Assert.That(value).IsEqualTo(expected)` pattern — async by default.
- TUnit test attribute is `[Test]`, not `[Fact]` or `[TestMethod]`.
- Guid.CreateVersion7() generates Version 7 GUIDs — version bits are in byte[7], bits 4-7.
- EventCreationTests.cs covers contract mapping, JSON deserialization of EventType enum, and Guid versioning.
- Build may fail if dependent projects (like Bethuya.Hybrid.Shared) have active Razor compilation errors, but test logic is sound.
- E2E test project (Playwright + MSTest) builds independently and successfully with proper data-test selectors.

