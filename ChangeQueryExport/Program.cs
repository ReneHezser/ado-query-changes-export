using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AdoQueries.Telemetry;
using PluginBase;

namespace AdoQueries
{
    class Program
    {
        public static void Main(string[] args)
        {
            // load environment variables from .env file
            DotEnv.Load(".env");

            var host = new HostBuilder()
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false);
                // This will load environments variables into the configuration object and is useful for cross-platform or container deployments. 
                // Furthermore, because Environment Variables are loaded after the appsettings.json file, any duplicate keys will replace the values from the appsettings.json file.
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                    builder.AddConsole();
                });
                services.AddHostedService<Worker>();

                // Application Insights
                // Add custom TelemetryInitializer
                services.AddSingleton<ITelemetryInitializer, AdoTelemetryInitializer>();
                // Add custom TelemetryProcessor
                services.AddApplicationInsightsTelemetryProcessor<AdoTelemetryProcessor>();
                // connection string is read from environment variable APPLICATIONINSIGHTS_CONNECTION_STRING
                services.AddApplicationInsightsTelemetryWorkerService();
            })
            .UseConsoleLifetime()
            .Build();

            using (host)
            {
                host.Run();
            }
        }
    }
}