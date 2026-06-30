using System.Net;
using System.Net.Http.Headers;
using Bethuya.Hybrid.Web.Auth;

namespace Hackmum.Bethuya.Tests.Auth;

/// <summary>
/// PR1 (C3): the Web app must propagate the signed-in user's access token to the Backend API on
/// every Refit call so the backend can enforce authorization on a real identity.
/// </summary>
public class BackendAccessTokenHandlerTests
{
    [Test]
    public async Task SendAsync_WithAccessToken_AddsBearerAuthorizationHeader()
    {
        var capturing = new CapturingHandler();
        var handler = new BackendAccessTokenHandler(new FakeTokenProvider("token-abc"))
        {
            InnerHandler = capturing
        };

        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://backend/api/events");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        await Assert.That(capturing.CapturedAuthorization).IsNotNull();
        await Assert.That(capturing.CapturedAuthorization!.Scheme).IsEqualTo("Bearer");
        await Assert.That(capturing.CapturedAuthorization.Parameter).IsEqualTo("token-abc");
    }

    [Test]
    public async Task SendAsync_WithoutAccessToken_DoesNotAddAuthorizationHeader()
    {
        var capturing = new CapturingHandler();
        var handler = new BackendAccessTokenHandler(new FakeTokenProvider(null))
        {
            InnerHandler = capturing
        };

        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://backend/api/events");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        await Assert.That(capturing.CapturedAuthorization).IsNull();
    }

    [Test]
    public async Task SendAsync_WithExistingAuthorizationHeader_DoesNotOverwrite()
    {
        var capturing = new CapturingHandler();
        var handler = new BackendAccessTokenHandler(new FakeTokenProvider("token-abc"))
        {
            InnerHandler = capturing
        };

        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://backend/api/events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "preset-token");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        await Assert.That(capturing.CapturedAuthorization!.Parameter).IsEqualTo("preset-token");
    }

    private sealed class FakeTokenProvider(string? token) : IBackendAccessTokenProvider
    {
        public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(token);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? CapturedAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedAuthorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
