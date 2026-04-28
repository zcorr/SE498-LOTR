namespace web_server.Services;

public class PremadeDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public int Class_id { get; set; }
    public int Race_id { get; set; }
    public System.Text.Json.JsonElement Stats { get; set; }
}
