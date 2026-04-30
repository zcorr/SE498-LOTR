using System.Text.Json;
using Npgsql;

namespace web_server.Services;

public class CharacterSheetService : ICharacterSheetService
{
    private readonly NpgsqlDataSource _db;

    public CharacterSheetService(NpgsqlDataSource db)
    {
        _db = db;
    }

    public async Task<int> SaveSheetAsync(int userId, SaveSheetRequest sheet)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO character_sheets (user_id, name, class_name, race_name, class_description, race_modifiers, stats)
            VALUES ($1, $2, $3, $4, $5, $6, $7::jsonb)
            RETURNING id
            """,
            conn);

        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(sheet.Name);
        cmd.Parameters.AddWithValue(sheet.ClassName);
        cmd.Parameters.AddWithValue(sheet.RaceName);
        cmd.Parameters.AddWithValue(sheet.ClassDescription);
        cmd.Parameters.AddWithValue(sheet.RaceModifiers);
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(sheet.Stats));

        var result = await cmd.ExecuteScalarAsync();
        return (int)result!;
    }

    public async Task<List<CharacterSheetSummary>> GetSheetsForUserAsync(int userId)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, name, class_name, race_name, stats::text, created_at
            FROM character_sheets
            WHERE user_id = $1
            ORDER BY created_at DESC
            """,
            conn);
        cmd.Parameters.AddWithValue(userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<CharacterSheetSummary>();
        while (await reader.ReadAsync())
        {
            var statsText = reader.GetString(4);
            var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(statsText)
                        ?? new Dictionary<string, int>();

            list.Add(new CharacterSheetSummary
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ClassName = reader.GetString(2),
                RaceName = reader.GetString(3),
                Stats = stats,
                CreatedAt = reader.GetDateTime(5),
            });
        }

        return list;
    }

    public async Task<CharacterSheetDetail?> GetSheetByIdAsync(int sheetId, int userId)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, name, class_name, race_name, class_description, race_modifiers, stats::text, created_at
            FROM character_sheets
            WHERE id = $1 AND user_id = $2
            """,
            conn);
        cmd.Parameters.AddWithValue(sheetId);
        cmd.Parameters.AddWithValue(userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var statsText = reader.GetString(6);
        var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(statsText)
                    ?? new Dictionary<string, int>();

        return new CharacterSheetDetail
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            ClassName = reader.GetString(2),
            RaceName = reader.GetString(3),
            ClassDescription = reader.IsDBNull(4) ? "" : reader.GetString(4),
            RaceModifiers = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Stats = stats,
            CreatedAt = reader.GetDateTime(7),
        };
    }

    public async Task<bool> DeleteSheetAsync(int sheetId, int userId)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM character_sheets WHERE id = $1 AND user_id = $2",
            conn);
        cmd.Parameters.AddWithValue(sheetId);
        cmd.Parameters.AddWithValue(userId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
}