namespace web_server.Services;

public interface ICharacterSheetService
{
	Task<int> SaveSheetAsync(int userId, SaveSheetRequest sheet);
	Task<List<CharacterSheetSummary>> GetSheetsForUserAsync(int userId);
	Task<CharacterSheetDetail?> GetSheetByIdAsync(int sheetId, int userId);
	Task<bool> DeleteSheetAsync(int sheetId, int userId);
}

public class SaveSheetRequest
{
	public string Name { get; set; } = string.Empty;
	public string ClassName { get; set; } = string.Empty;
	public string RaceName { get; set; } = string.Empty;
	public string ClassDescription { get; set; } = string.Empty;
	public string RaceModifiers { get; set; } = string.Empty;
	public Dictionary<string, int> Stats { get; set; } = new();
}

public class CharacterSheetSummary
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string ClassName { get; set; } = string.Empty;
	public string RaceName { get; set; } = string.Empty;
	public Dictionary<string, int> Stats { get; set; } = new();
	public DateTime CreatedAt { get; set; }
}

public class CharacterSheetDetail : CharacterSheetSummary
{
	public string ClassDescription { get; set; } = string.Empty;
	public string RaceModifiers { get; set; } = string.Empty;
}