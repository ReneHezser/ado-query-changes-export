using McMaster.NETCore.Plugins;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using PluginBase;

namespace AdoQueries
{
   public class Worker : BackgroundService
   {
      private static string version = "1.0.10";

      private readonly ILogger<Worker> _logger;
      private TelemetryClient _telemetryClient;
      private IHostApplicationLifetime _hostLifetime;

      public Worker(IHostApplicationLifetime hostLifetime, ILogger<Worker> logger, TelemetryClient tc)
      {
         _logger = logger;
         _telemetryClient = tc;
         _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         while (!stoppingToken.IsCancellationRequested)
         {
            try
            {
               // By default only Warning of above is captured.
               // However the following Info level will be captured by ApplicationInsights,
               _logger.LogInformation("Worker running version {version} at: {time}", version, DateTimeOffset.Now);

               // Paths from where plugins are loaded
               string[] pluginPaths = new string[]
               {
               Directory.GetCurrentDirectory(),
               Path.Combine(new string[]{ Directory.GetCurrentDirectory() , "Plugins"})
               };

               var commands = LoadAndExecutePlugins(pluginPaths);
               commands.ForEach(command => _logger.LogInformation($"Found plugin '{command.Name} - {command.Description}'"));
               if (commands.Count() == 0) throw new Exception("No plugins found in " + pluginPaths.Aggregate((a, b) => a + ", " + b));

               List<IReportItem> workItems;
               int queryDays;
               if (!int.TryParse(Environment.GetEnvironmentVariable("QUERY_DAYS"), out queryDays))
                  throw new ArgumentOutOfRangeException("QUERY_DAYS", "QUERY_DAYS must be a valid integer.");

               // load Workitems
               using (_telemetryClient.StartOperation<RequestTelemetry>("Query Workitems in " + Environment.GetEnvironmentVariable("PROJECT")))
               {
                  var queryExecutor = new QueryExecutor(_logger,
                                     Environment.GetEnvironmentVariable("ORGANIZATION"),
                                     Environment.GetEnvironmentVariable("PROJECT"),
                                     Environment.GetEnvironmentVariable("PERSONAL_ACCESS_TOKEN"),
                                     queryDays
                                     );
                  var task = queryExecutor.QueryWorkitems();
                  task.Wait();
                  workItems = task.Result;
               }

               // do something with the workitems for each Plugin
               var reporting = new Dictionary<string, int>();
               foreach (IPlugin command in commands)
               {
                  using (_logger.BeginScope("Worker.PluginExecution"))
                  using (_telemetryClient.StartOperation<RequestTelemetry>("Command: " + command.Name))
                  {
                     try
                     {
                        _logger.LogDebug($"Executing '{command.Name} - {command.Description}'");
                        int affectedItems = command.Execute(workItems);
                        _logger.LogInformation($"Executed '{command.Name} - {command.Description}' on {affectedItems} items");
                        reporting.Add(command.Name, affectedItems);
                     }
                     catch (Exception ex)
                     {
                        _logger.LogError(ex, $"Error executing '{command.Name}': {ex.Message}");
                        _telemetryClient.TrackEvent($"UBS-ADO Sync Worker - Error",
                           metrics: new Dictionary<string, double> { { "Workitems", workItems.Count } },
                           properties: new Dictionary<string, string> { { command.Name, ex.Message } }
                        );
                     }
                     finally
                     {
                        // ensure all telemetry is flushed before exiting
                        _telemetryClient.Flush();
                     }
                  }
               }

               var metrics = new Dictionary<string, double> { { "Workitems", workItems.Count }, { "Plugins", commands.Count() } };
               // add metrics for each plugin
               foreach (var item in reporting) metrics.Add(item.Key, item.Value);
               _telemetryClient.TrackEvent($"UBS-ADO Sync Worker - Completed",
                  metrics: metrics,
                  properties: new Dictionary<string, string> {
                           { "Version", version },
                           { "QueryDays", queryDays.ToString() } }
               );
               _telemetryClient.Flush();
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, $"Error executing worker: {ex.Message}");
               _telemetryClient.TrackEvent($"UBS-ADO Sync Worker - Error", properties: new Dictionary<string, string> { { "Error", ex.Message } });
               _telemetryClient.Flush();
            }

            await Task.Delay(1000, stoppingToken);
            _hostLifetime.StopApplication();
         }
      }

      private IEnumerable<IPlugin> LoadAndExecutePlugins(string[] pluginPaths)
      {
         IList<IPlugin> commands = new List<IPlugin>();

         foreach (var pluginPath in pluginPaths)
         {
            // load plugins
            var plugins = LoadPlugin(pluginPath);
            // create instances of the plugins
            foreach (var loader in plugins)
            {
               foreach (var pluginType in loader
                   .LoadDefaultAssembly()
                   .GetTypes()
                   .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract))
               {
                  // This assumes the implementation of IPlugin has a parameterless constructor
                  IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType);
                  plugin.Logger = _logger;
                  commands.Add(plugin);
                  Console.WriteLine($"Created plugin instance '{plugin.Name}'.");
               }
            }
         }

         return commands;
      }

      private List<PluginLoader> LoadPlugin(string pluginLocation)
      {
         _logger.LogInformation($"Loading commands from: {pluginLocation}");

         // Plugins are dll files. Dependencies for plugins are placed in the same folder and are dlls as well
         FileInfo[] pluginFiles = new DirectoryInfo(pluginLocation).GetFiles("*.dll");
         _logger.LogDebug("Found {count} dll files", pluginFiles.Length);
         var plugins = new List<PluginLoader>();

         foreach (var pluginFile in pluginFiles)
         {
            var loader = PluginLoader.CreateFromAssemblyFile(pluginFile.FullName, sharedTypes: new[] { typeof(IPlugin) });
            plugins.Add(loader);
         }

         return plugins;
      }
   }
}