using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_server.Services;

namespace web_server.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/banner-ads")]
public class BannerAdsController : ControllerBase
{
    private readonly IJurassicAdsClient _adsClient;

    public BannerAdsController(IJurassicAdsClient adsClient)
    {
        _adsClient = adsClient;
    }

    // GET /api/banner-ads/movies — proxies the Jurassic API's poster catalog.
    // Anonymous + same-origin so the browser banner widget can call it without
    // colliding with the Jurassic API's CORS allow-list.
    [HttpGet("movies")]
    public async Task<IActionResult> GetMoviePosters(CancellationToken cancellationToken)
    {
        var posters = await _adsClient.GetPostersAsync(cancellationToken);
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(posters);
    }
}
