namespace web_server.Services;

public class PremadeListResponseDTO
{
    public List<PremadeDTO> Items { get; set; } = [];
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}
