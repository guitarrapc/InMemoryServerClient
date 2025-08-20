using Aws.GameLift.Server;

namespace InMemoryServer.Services;

public static class GameLiftServerSdkHelper
{
    public static bool InitSdk(ILogger logger)
    {
        var outcome = GameLiftServerAPI.InitSDK();
        if (!outcome.Success)
        {
            logger.LogError("GameLiftServerAPI.InitSDK failed: {Error}", outcome.Error.ToString());
            return false;
        }
        logger.LogInformation("GameLiftServerAPI.InitSDK succeeded");
        return true;
    }

    public static bool ProcessReady(ProcessParameters parameters, ILogger logger)
    {
        var outcome = GameLiftServerAPI.ProcessReady(parameters);
        if (!outcome.Success)
        {
            logger.LogError("GameLiftServerAPI.ProcessReady failed: {Error}", outcome.Error.ToString());
            return false;
        }
        logger.LogInformation("GameLiftServerAPI.ProcessReady succeeded");
        return true;
    }

    public static void ProcessEnding(ILogger logger)
    {
        GameLiftServerAPI.ProcessEnding();
        logger.LogInformation("GameLiftServerAPI.ProcessEnding called");
    }

    public static void ActivateGameSession(string gameSessionId, ILogger logger)
    {
        GameLiftServerAPI.ActivateGameSession();
        logger.LogInformation("GameLiftServerAPI.ActivateGameSession called for {GameSessionId}", gameSessionId);
    }

    public static bool HealthCheck() => true;
}
