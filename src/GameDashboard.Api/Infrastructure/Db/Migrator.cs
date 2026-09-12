using DbUp;
using DbUp.Engine;

namespace GameDashboard.Api.Infrastructure.Db;

public static class Migrator
{
    public static int Run(string connectionString)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(Migrator).Assembly)
            .WithTransaction()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            Console.Error.WriteLine(result.Error);
            return 1;
        }
        Console.WriteLine("Migration completed successfully.");
        return 0;
    }
}
