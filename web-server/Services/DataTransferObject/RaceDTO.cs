namespace web_server.Services;

public class RaceDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Modifiers { get; set; }
}