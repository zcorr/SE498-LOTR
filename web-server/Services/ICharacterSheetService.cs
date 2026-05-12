namespace web_server.Services;

public class AttackEntry
{
    public string Name { get; set; } = string.Empty;
    public string AtkBonus { get; set; } = string.Empty;
    public string Damage { get; set; } = string.Empty;
}

public interface ICharacterSheetService
{
    Task<int> SaveSheetAsync(int userId, SaveSheetRequest sheet);
    Task<List<CharacterSheetSummary>> GetSheetsForUserAsync(int userId);
    Task<CharacterSheetDetail?> GetSheetByIdAsync(int sheetId, int userId);
    Task<bool> UpdateSheetAsync(int sheetId, int userId, UpdateSheetRequest update);
    Task<bool> DeleteSheetAsync(int sheetId, int userId);
}

public class SaveSheetRequest
{
    public string Name { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string RaceName { get; set; } = string.Empty;
    public string ClassDescription { get; set; } = string.Empty;
    public string RaceModifiers { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
    public string PersonalityTraits { get; set; } = string.Empty;
    public string Ideals { get; set; } = string.Empty;
    public string Bonds { get; set; } = string.Empty;
    public string Flaws { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public string FeaturesTraits { get; set; } = string.Empty;
    public List<AttackEntry> Attacks { get; set; } = new();
    public Dictionary<string, int> Stats { get; set; } = new();
}

public class UpdateSheetRequest
{
    public string Name { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
    public string PersonalityTraits { get; set; } = string.Empty;
    public string Ideals { get; set; } = string.Empty;
    public string Bonds { get; set; } = string.Empty;
    public string Flaws { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public string FeaturesTraits { get; set; } = string.Empty;
    public List<AttackEntry> Attacks { get; set; } = new();
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
    public string Background { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty;
    public string PersonalityTraits { get; set; } = string.Empty;
    public string Ideals { get; set; } = string.Empty;
    public string Bonds { get; set; } = string.Empty;
    public string Flaws { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public string FeaturesTraits { get; set; } = string.Empty;
    public List<AttackEntry> Attacks { get; set; } = new();
}