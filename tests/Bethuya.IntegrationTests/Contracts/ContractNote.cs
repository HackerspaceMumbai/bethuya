// BP6 — Anti-regression contract strategy:
// API contract types (request/response models) are deliberately duplicated in this namespace
// rather than referencing Hackmum.Bethuya.Backend directly.
//
// Why: A rename or structural change in the Backend project that breaks this test project
// signals a breaking API change to consumers. It's an intentional compile-time safety net.
//
// Add request/response record types here as you write tests that consume the backend API.
namespace Bethuya.IntegrationTests.Contracts;
