using System.Text.Json;
using System.Text.Json.Serialization;

namespace web_server.Services;

public class JurassicAdsClient : IJurassicAdsClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<JurassicAdsClient> _logger;

    public JurassicAdsClient(HttpClient httpClient, ILogger<JurassicAdsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JurassicMoviePoster>> GetPostersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/movies/posters", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Jurassic /movies/posters returned {Status}", response.StatusCode);
                return Array.Empty<JurassicMoviePoster>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var posters = await JsonSerializer.DeserializeAsync<List<JurassicPosterPayload>>(
                stream, SerializerOptions, cancellationToken);

            if (posters is null)
            {
                return Array.Empty<JurassicMoviePoster>();
            }

            return posters
                .Where(p => !string.IsNullOrWhiteSpace(p.PosterUrl) && !string.IsNullOrWhiteSpace(p.Title))
                .Select(p => new JurassicMoviePoster
                {
                    MovieId = p.MovieId ?? string.Empty,
                    Title = p.Title ?? string.Empty,
                    PosterUrl = p.PosterUrl ?? string.Empty
                })
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogInformation(ex, "Jurassic ads service unreachable; serving no banner ads this request");
            return Array.Empty<JurassicMoviePoster>();
        }
    }

    private sealed class JurassicPosterPayload
    {
        [JsonPropertyName("movie_id")]
        public string? MovieId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("poster_url")]
        public string? PosterUrl { get; set; }
    }
}
