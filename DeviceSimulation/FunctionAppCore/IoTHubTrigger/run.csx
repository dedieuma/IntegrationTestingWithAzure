using System;

public static string Run(string myIoTHubMessage, ILogger log)
{
    log.LogInformation($"C# Queue trigger function processed: {myIoTHubMessage}");

    return myIoTHubMessage;
}
