namespace web_server.Services;

public class JurassicMoviePoster
{
    public string MovieId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
}

public interface IJurassicAdsClient
{
    Task<IReadOnlyList<JurassicMoviePoster>> GetPostersAsync(CancellationToken cancellationToken = default);
}
