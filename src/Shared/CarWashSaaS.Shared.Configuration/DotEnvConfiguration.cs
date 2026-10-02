namespace CarWashSaaS.Shared.Configuration;

public static class DotEnvConfiguration
{
    private const string ConnectionStringVariable = "ConnectionStrings__CarWashSaaS";
    private const string SolutionFileName = "CarWashSaaS.sln";

    public static void LoadFromRepository()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            return;
        }

        var envFile = Path.Combine(repositoryRoot, ".env");
        if (File.Exists(envFile))
        {
            DotNetEnv.Env.NoClobber().Load(envFile);
        }
    }

    public static string GetRequiredConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable)
        ?? throw new InvalidOperationException(
            $"Set {ConnectionStringVariable} in the environment or in the repository .env file.");

    private static string? FindRepositoryRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
                {
                    return directory.FullName;
                }
            }
        }

        return null;
    }
}
