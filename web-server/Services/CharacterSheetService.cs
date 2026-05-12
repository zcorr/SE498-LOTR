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
            INSERT INTO character_sheets
              (user_id, name, class_name, race_name, class_description, race_modifiers,
               background, player_name, alignment, personality_traits, ideals, bonds, flaws,
               equipment, features_traits, stats)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16::jsonb)
            RETURNING id
            """,
            conn);

        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(sheet.Name);
        cmd.Parameters.AddWithValue(sheet.ClassName);
        cmd.Parameters.AddWithValue(sheet.RaceName);
        cmd.Parameters.AddWithValue(sheet.ClassDescription);
        cmd.Parameters.AddWithValue(sheet.RaceModifiers);
        cmd.Parameters.AddWithValue(sheet.Background);
        cmd.Parameters.AddWithValue(sheet.PlayerName);
        cmd.Parameters.AddWithValue(sheet.Alignment);
        cmd.Parameters.AddWithValue(sheet.PersonalityTraits);
        cmd.Parameters.AddWithValue(sheet.Ideals);
        cmd.Parameters.AddWithValue(sheet.Bonds);
        cmd.Parameters.AddWithValue(sheet.Flaws);
        cmd.Parameters.AddWithValue(sheet.Equipment);
        cmd.Parameters.AddWithValue(sheet.FeaturesTraits);
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
            SELECT id, name, class_name, race_name, class_description, race_modifiers,
                   background, player_name, alignment, personality_traits, ideals, bonds, flaws,
                   equipment, features_traits, stats::text, created_at
            FROM character_sheets
            WHERE id = $1 AND user_id = $2
            """,
            conn);
        cmd.Parameters.AddWithValue(sheetId);
        cmd.Parameters.AddWithValue(userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var statsText = reader.GetString(15);
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
            Background = reader.IsDBNull(6) ? "" : reader.GetString(6),
            PlayerName = reader.IsDBNull(7) ? "" : reader.GetString(7),
            Alignment = reader.IsDBNull(8) ? "" : reader.GetString(8),
            PersonalityTraits = reader.IsDBNull(9) ? "" : reader.GetString(9),
            Ideals = reader.IsDBNull(10) ? "" : reader.GetString(10),
            Bonds = reader.IsDBNull(11) ? "" : reader.GetString(11),
            Flaws = reader.IsDBNull(12) ? "" : reader.GetString(12),
            Equipment = reader.IsDBNull(13) ? "" : reader.GetString(13),
            FeaturesTraits = reader.IsDBNull(14) ? "" : reader.GetString(14),
            Stats = stats,
            CreatedAt = reader.GetDateTime(16),
        };
    }

    public async Task<bool> UpdateSheetAsync(int sheetId, int userId, UpdateSheetRequest update)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE character_sheets
            SET name=$3, background=$4, player_name=$5, alignment=$6,
                personality_traits=$7, ideals=$8, bonds=$9, flaws=$10,
                equipment=$11, features_traits=$12, stats=$13::jsonb
            WHERE id=$1 AND user_id=$2
            """,
            conn);

        cmd.Parameters.AddWithValue(sheetId);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(update.Name);
        cmd.Parameters.AddWithValue(update.Background);
        cmd.Parameters.AddWithValue(update.PlayerName);
        cmd.Parameters.AddWithValue(update.Alignment);
        cmd.Parameters.AddWithValue(update.PersonalityTraits);
        cmd.Parameters.AddWithValue(update.Ideals);
        cmd.Parameters.AddWithValue(update.Bonds);
        cmd.Parameters.AddWithValue(update.Flaws);
        cmd.Parameters.AddWithValue(update.Equipment);
        cmd.Parameters.AddWithValue(update.FeaturesTraits);
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(update.Stats));

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
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