using Npgsql;

namespace nInvoices.Infrastructure.Tests.PostgreSql;

/// <summary>
/// A PostgreSQL schema as sorted text lines, so two databases can be compared and the difference
/// read: one line per column (type, nullability, default, identity), constraint (primary/foreign
/// key, unique, check, with its definition) and index (with its definition).
/// </summary>
internal static class PostgreSqlSchema
{
    public const string MigrationsHistory = "__EFMigrationsHistory";

    public static async Task<IReadOnlyList<string>> SnapshotAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var lines = new List<string>();
        lines.AddRange(await QueryAsync(connection, """
            SELECT format('column %s.%s %s%s %s default=%s identity=%s',
                       table_name, column_name, data_type,
                       COALESCE('(' || character_maximum_length || ')', ''),
                       CASE is_nullable WHEN 'YES' THEN 'null' ELSE 'not-null' END,
                       COALESCE(column_default, '-'), is_identity)
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'
            """, cancellationToken));
        lines.AddRange(await QueryAsync(connection, """
            SELECT format('constraint %s.%s %s', c.conrelid::regclass, c.conname, pg_get_constraintdef(c.oid))
            FROM pg_constraint c
            WHERE c.connamespace = 'public'::regnamespace AND c.conrelid::regclass::text <> '"__EFMigrationsHistory"'
            """, cancellationToken));
        lines.AddRange(await QueryAsync(connection, """
            SELECT format('index %s.%s %s', tablename, indexname, indexdef)
            FROM pg_indexes
            WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
            """, cancellationToken));

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    /// <summary>Runs a script as <c>psql</c> would (BEGIN/COMMIT and DO blocks included).</summary>
    public static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>What differs between two snapshots, as "- only in expected" / "+ only in actual" lines.</summary>
    public static IReadOnlyList<string> Diff(IReadOnlyList<string> expected, IReadOnlyList<string> actual) =>
        expected.Except(actual).Select(l => "- " + l)
            .Concat(actual.Except(expected).Select(l => "+ " + l))
            .ToList();

    private static async Task<IReadOnlyList<string>> QueryAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lines = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            lines.Add(reader.GetString(0));
        return lines;
    }
}
