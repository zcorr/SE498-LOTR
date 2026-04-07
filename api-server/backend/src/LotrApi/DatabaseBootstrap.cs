using Npgsql;

namespace LotrApi;

public static class DatabaseBootstrap
{
    public static async Task ApplyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var assemblyDir = Path.GetDirectoryName(typeof(DatabaseBootstrap).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var schemaDir = Path.Combine(assemblyDir, "database", "schema");
        if (!Directory.Exists(schemaDir))
            throw new DirectoryNotFoundException($"Schema directory not found: {schemaDir}");

        var files = Directory.GetFiles(schemaDir, "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        foreach (var file in files)
        {
            var sql = await File.ReadAllTextAsync(file, cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
