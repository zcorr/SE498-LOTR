namespace web_server.Services;

public class GeneratedCharacterSheetDTO
{
    public int ClassId { get; set; }
    public int RaceId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string RaceName { get; set; } = string.Empty;
    public string ClassDescription { get; set; } = string.Empty;
    public string RaceModifiers { get; set; } = string.Empty;
    public System.Collections.Generic.Dictionary<string, int> Stats { get; set; } = new();
}