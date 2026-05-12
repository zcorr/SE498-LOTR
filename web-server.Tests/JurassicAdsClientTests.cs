// =============================================================================
// JurassicAdsClientTests.cs — Unit tests for the Jurassic ads HTTP client.
//
// These don't spin up the MVC app. They wrap JurassicAdsClient around a
// fake HttpMessageHandler so we can drive every branch (success, non-2xx,
// malformed JSON, transport failure, filtering) without a real server.
// =============================================================================

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using web_server.Services;
using Xunit;

namespace web_server.Tests;

public class JurassicAdsClientTests
{
    // ── Fake HttpMessageHandler ──
    // Lets each test inject a canned response (or throw) without binding to
    // any concrete mocking library quirk for protected members.
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            try
            {
                return Task.FromResult(_responder(request));
            }
            catch (Exception ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }

    private static JurassicAdsClient CreateClient(StubHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5080"),
        };
        return new JurassicAdsClient(http, NullLogger<JurassicAdsClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    [Fact]
    public async Task GetPostersAsync_CallsMoviesPostersEndpoint()
    {
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "[]"));
        var client = CreateClient(handler);

        await client.GetPostersAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/movies/posters", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetPostersAsync_DeserializesSnakeCasePayload()
    {
        const string body = """
            [
              { "movie_id": "jp-1", "title": "Jurassic Park", "poster_url": "https://cdn/jp1.jpg" }
            ]
            """;
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, body));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        var poster = Assert.Single(posters);
        Assert.Equal("jp-1", poster.MovieId);
        Assert.Equal("Jurassic Park", poster.Title);
        Assert.Equal("https://cdn/jp1.jpg", poster.PosterUrl);
    }

    [Fact]
    public async Task GetPostersAsync_FiltersOutPostersWithMissingTitle()
    {
        const string body = """
            [
              { "movie_id": "ok",     "title": "Jurassic Park",       "poster_url": "https://cdn/ok.jpg" },
              { "movie_id": "no-ttl", "title": "",                    "poster_url": "https://cdn/x.jpg"  },
              { "movie_id": "null",   "title": null,                  "poster_url": "https://cdn/y.jpg"  },
              { "movie_id": "spaces", "title": "   ",                 "poster_url": "https://cdn/z.jpg"  }
            ]
            """;
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, body));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        var poster = Assert.Single(posters);
        Assert.Equal("ok", poster.MovieId);
    }

    [Fact]
    public async Task GetPostersAsync_FiltersOutPostersWithMissingPosterUrl()
    {
        const string body = """
            [
              { "movie_id": "ok",      "title": "Jurassic Park",  "poster_url": "https://cdn/ok.jpg" },
              { "movie_id": "no-url",  "title": "Lost World",     "poster_url": ""                   },
              { "movie_id": "null",    "title": "JP3",            "poster_url": null                 }
            ]
            """;
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, body));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        var poster = Assert.Single(posters);
        Assert.Equal("ok", poster.MovieId);
    }

    [Fact]
    public async Task GetPostersAsync_MissingMovieId_DefaultsToEmptyString()
    {
        const string body = """
            [
              { "title": "Jurassic Park", "poster_url": "https://cdn/jp1.jpg" }
            ]
            """;
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, body));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        var poster = Assert.Single(posters);
        Assert.Equal(string.Empty, poster.MovieId);
        Assert.Equal("Jurassic Park", poster.Title);
    }

    [Fact]
    public async Task GetPostersAsync_NonSuccessStatus_ReturnsEmpty()
    {
        var handler = new StubHandler(_ =>
            JsonResponse(HttpStatusCode.InternalServerError, "boom"));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        Assert.Empty(posters);
    }

    [Fact]
    public async Task GetPostersAsync_NotFound_ReturnsEmpty()
    {
        var handler = new StubHandler(_ =>
            JsonResponse(HttpStatusCode.NotFound, "[]"));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        Assert.Empty(posters);
    }

    [Fact]
    public async Task GetPostersAsync_NullJsonBody_ReturnsEmpty()
    {
        // JsonSerializer happily deserializes the literal `null` to a null
        // List<T>. The client guards against that and returns an empty array.
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "null"));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        Assert.Empty(posters);
    }

    [Fact]
    public async Task GetPostersAsync_MalformedJson_ReturnsEmpty()
    {
        // Malformed JSON throws JsonException, which the client catches and
        // turns into an empty list so banner failures never break the page.
        var handler = new StubHandler(_ =>
            JsonResponse(HttpStatusCode.OK, "{ not valid json"));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        Assert.Empty(posters);
    }

    [Fact]
    public async Task GetPostersAsync_HttpRequestException_ReturnsEmpty()
    {
        var handler = new StubHandler(_ =>
            throw new HttpRequestException("connection refused"));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        Assert.Empty(posters);
    }

    [Fact]
    public async Task GetPostersAsync_TaskCanceled_ReturnsEmpty()
    {
        // Models the request timing out / being canceled by an upstream
        // CancellationToken — the client should still return an empty list
        // rather than throw and break the page render.
        var handler = new StubHandler(_ => throw new TaskCanceledException());
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        Assert.Empty(posters);
    }

    [Fact]
    public async Task GetPostersAsync_EmptyJsonArray_ReturnsEmpty()
    {
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "[]"));
        var client = CreateClient(handler);

        var posters = await client.GetPostersAsync();

        Assert.Empty(posters);
    }
}
