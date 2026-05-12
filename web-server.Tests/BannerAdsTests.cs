// =============================================================================
// BannerAdsTests.cs — Integration tests for /api/banner-ads/*
//
// Reuses LotrWebAppFactory (see EndpointTests.cs), which substitutes
// IJurassicAdsClient with a Moq mock so we never reach the real Jurassic API.
//
// NOTE on ordering: LotrWebAppFactory is shared across this class via
// IClassFixture, so the Moq instance is shared too. Each test resets and
// re-applies its own setup *after* CreateClient() — the factory's
// ConfigureWebHost installs default setups lazily on the first CreateClient
// call, which would otherwise clobber any pre-call configuration.
// =============================================================================

using System.Net;
using System.Net.Http.Json;
using Moq;
using web_server.Services;
using Xunit;

namespace web_server.Tests;

public class BannerAdsTests : IClassFixture<LotrWebAppFactory>
{
    private readonly LotrWebAppFactory _factory;

    public BannerAdsTests(LotrWebAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    private void SetPostersResult(IReadOnlyList<JurassicMoviePoster> posters)
    {
        _factory.MockJurassicAdsClient.Reset();
        _factory.MockJurassicAdsClient
            .Setup(x => x.GetPostersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(posters);
    }

    [Fact]
    public async Task GetMoviePosters_WithoutAuth_Returns200()
    {
        // The banner endpoint is decorated with [AllowAnonymous] so the
        // browser widget can call it on public pages (login, register).
        var client = CreateClient();
        SetPostersResult(Array.Empty<JurassicMoviePoster>());

        var response = await client.GetAsync("/api/banner-ads/movies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMoviePosters_SetsPublicCacheControlHeader()
    {
        // The controller explicitly sets Cache-Control to allow a 60-second
        // shared cache. This keeps the proxy from hammering Jurassic on every
        // page load.
        var client = CreateClient();
        SetPostersResult(Array.Empty<JurassicMoviePoster>());

        var response = await client.GetAsync("/api/banner-ads/movies");

        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl!.Public);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.CacheControl.MaxAge);
    }

    [Fact]
    public async Task GetMoviePosters_ReturnsEmptyArray_WhenClientHasNoPosters()
    {
        var client = CreateClient();
        SetPostersResult(Array.Empty<JurassicMoviePoster>());

        var response = await client.GetAsync("/api/banner-ads/movies");

        response.EnsureSuccessStatusCode();
        var posters = await response.Content.ReadFromJsonAsync<List<JurassicMoviePoster>>();
        Assert.NotNull(posters);
        Assert.Empty(posters!);
    }

    [Fact]
    public async Task GetMoviePosters_ReturnsPosters_FromClient()
    {
        var client = CreateClient();
        SetPostersResult(new List<JurassicMoviePoster>
        {
            new() { MovieId = "jp-1", Title = "Jurassic Park", PosterUrl = "https://cdn/jp1.jpg" },
            new() { MovieId = "jp-2", Title = "The Lost World", PosterUrl = "https://cdn/jp2.jpg" },
        });

        var response = await client.GetAsync("/api/banner-ads/movies");

        response.EnsureSuccessStatusCode();
        var posters = await response.Content.ReadFromJsonAsync<List<JurassicMoviePoster>>();
        Assert.NotNull(posters);
        Assert.Equal(2, posters!.Count);
        Assert.Equal("jp-1", posters[0].MovieId);
        Assert.Equal("Jurassic Park", posters[0].Title);
        Assert.Equal("https://cdn/jp1.jpg", posters[0].PosterUrl);
    }

    [Fact]
    public async Task GetMoviePosters_DelegatesToJurassicAdsClient()
    {
        var client = CreateClient();
        SetPostersResult(Array.Empty<JurassicMoviePoster>());

        await client.GetAsync("/api/banner-ads/movies");

        _factory.MockJurassicAdsClient.Verify(
            x => x.GetPostersAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
