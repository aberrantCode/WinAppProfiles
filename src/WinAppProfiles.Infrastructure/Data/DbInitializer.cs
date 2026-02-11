using Dapper;

namespace WinAppProfiles.Infrastructure.Data;

public sealed class DbInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DbInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS profiles (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                is_default INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS profile_items (
                id TEXT PRIMARY KEY,
                profile_id TEXT NOT NULL,
                target_type INTEGER NOT NULL,
                display_name TEXT NOT NULL,
                process_name TEXT NULL,
                executable_path TEXT NULL,
                service_name TEXT NULL,
                desired_state INTEGER NOT NULL,
                is_reviewed INTEGER NOT NULL,
                FOREIGN KEY(profile_id) REFERENCES profiles(id)
            );

            CREATE TABLE IF NOT EXISTS apply_runs (
                id TEXT PRIMARY KEY,
                profile_id TEXT NOT NULL,
                started_at TEXT NOT NULL,
                finished_at TEXT NOT NULL,
                status TEXT NOT NULL,
                summary_json TEXT NOT NULL,
                FOREIGN KEY(profile_id) REFERENCES profiles(id)
            );

            CREATE TABLE IF NOT EXISTS apply_run_items (
                id TEXT PRIMARY KEY,
                run_id TEXT NOT NULL,
                profile_item_id TEXT NOT NULL,
                requested_state INTEGER NOT NULL,
                actual_state INTEGER NULL,
                success INTEGER NOT NULL,
                error_code TEXT NULL,
                error_message TEXT NULL,
                FOREIGN KEY(run_id) REFERENCES apply_runs(id)
            );
            """);
    }
}
