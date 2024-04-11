using Azure.Identity;
using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration.Extensions;
using ILogger=Serilog.ILogger;

public static class AzureAppConfigurationExtensions
{
  public static async Task AddAzureAppConfigurationAsync(this WebApplicationBuilder webApplicationBuilder, ILogger logger)
  {
    IConfigurationRefresher refresher = default!;

    var configuration = webApplicationBuilder.Configuration;
    // TODO: Use a guard class instead
    var appConfigEndpoint = configuration.GetValue<string>("AppConfigEndpoint") ?? string.Empty;

    webApplicationBuilder.Configuration.AddAzureAppConfiguration(options =>
    {
      options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential());
      options.Select("farkle:*", "local");
      options.TrimKeyPrefix("farkle:");
      options.ConfigureRefresh(refresh =>
      {
        refresh.SetCacheExpiration(TimeSpan.FromDays(1));
        refresh.Register("sentinel", true);
      });
      options.ConfigureKeyVault(kv => { kv.SetCredential(new DefaultAzureCredential()); });
      refresher = options.GetRefresher();
    });

    await RegisterConfigurationRefresherEventHandlerAsync(configuration, refresher);
    
    logger.Information($"Using configuration sentinel version: {configuration["Sentinel"]}");
  }
  
  // TODO: split the call to this private method. Make it public and call directly from program after the build
  private static async Task RegisterConfigurationRefresherEventHandlerAsync(IConfiguration config, IConfigurationRefresher refresher)
  {
    await refresher.TryRefreshAsync();

    var serviceBusEndpoint  = config.GetValue<string>("ConfigServiceBusEndpoint");
    var serviceBusQueue     = config.GetValue<string>("ConfigServiceBusQueue");
    var serviceBusClient    = new ServiceBusClient(serviceBusEndpoint, new DefaultAzureCredential());
    var serviceBusProcessor = serviceBusClient.CreateProcessor(serviceBusQueue);

    serviceBusProcessor.ProcessMessageAsync += (processMessageEventArgs) =>
    {
      // Build EventGridEvent from notification message
      var eventGridEvent = EventGridEvent.Parse(processMessageEventArgs.Message.Body);
      // Create PushNotification from eventGridEvent
      eventGridEvent.TryCreatePushNotification(out var pushNotification);

      // Prompt Configuration Refresh based on the PushNotification
      refresher.ProcessPushNotification(pushNotification);

      return Task.CompletedTask;
    };

    serviceBusProcessor.ProcessErrorAsync += (exceptionargs) =>
    {
      Console.WriteLine($"{exceptionargs.Exception}");
      return Task.CompletedTask;
    };

    await serviceBusProcessor.StartProcessingAsync();
  }

}
