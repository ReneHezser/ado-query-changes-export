using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

            var host = CreateHostBuilder(args).UseConsoleLifetime().Build();
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();

                    // Application Insights

                    // Add custom TelemetryInitializer
                    services.AddSingleton<ITelemetryInitializer, AdoTelemetryInitializer>();

                    // Add custom TelemetryProcessor
                    services.AddApplicationInsightsTelemetryProcessor<AdoTelemetryProcessor>();

                    // instrumentation key is read automatically from appsettings.json
                    services.AddApplicationInsightsTelemetryWorkerService();
                });
    }
}