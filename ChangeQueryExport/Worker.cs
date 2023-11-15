using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using PluginBase;
using System.Reflection;

namespace AdoQueries
{
   public class Worker : BackgroundService
   {
      private static string version = "1.0.7";

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
            // By default only Warning of above is captured.
            // However the following Info level will be captured by ApplicationInsights,
            _logger.LogInformation("Worker running version {version} at: {time}", version, DateTimeOffset.Now);

            var commands = LoadAndExecutePlugins();
            commands.ForEach(command => _logger.LogInformation($"Found plugin '{command.Name} - {command.Description}'"));

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
            foreach (IPlugin command in commands)
            {
               using (_logger.BeginScope("Worker.PluginExecution"))
               using (_telemetryClient.StartOperation<RequestTelemetry>("Command: " + command.Name))
               {
                  try
                  {
                     _logger.LogDebug($"Executing '{command.Name} - {command.Description}'");
                     command.Execute(workItems);
                  }
                  catch (Exception ex)
                  {
                     _logger.LogError(ex, $"Error executing '{command.Name} - {command.Description}'");
                  }
                  finally
                  {
                     // ensure all telemetry is flushed before exiting
                     _telemetryClient.Flush();
                  }
               }
            }

            _telemetryClient.TrackEvent($"ADO Sync Worker completed {workItems.Count} items", metrics: new Dictionary<string, double> { { "Workitems", workItems.Count } });
            _telemetryClient.Flush();

            await Task.Delay(1000, stoppingToken);
            _hostLifetime.StopApplication();
         }
      }

      private IEnumerable<IPlugin> LoadAndExecutePlugins()
      {
         // Paths from where plugins are loaded
         string[] pluginPaths = new string[]
         {
            Path.Combine(new string[]{ Directory.GetCurrentDirectory() , "Plugins"})
         };

         IEnumerable<IPlugin> commands = pluginPaths.SelectMany(pluginPath =>
         {
            IEnumerable<Assembly> pluginAssemblies = LoadPlugin(pluginPath);
            return CreateCommands(pluginAssemblies);
         }).ToList();

         return commands;
      }

      private IEnumerable<Assembly> LoadPlugin(string pluginLocation)
      {
         _logger.LogInformation($"Loading commands from: {pluginLocation}");
         var loadContext = new PluginLoadContext(pluginLocation);

         // Plugins are dll files. Dependencies for plugins are placed in the same folder and are dlls as well
         FileInfo[] pluginFiles = new DirectoryInfo(pluginLocation).GetFiles("*.dll");
         _logger.LogDebug("Found {count} dll files", pluginFiles.Length);

         foreach (var pluginFile in pluginFiles)
         {
            _logger.LogDebug("Trying to load {pluginFile} as IPlugin", pluginFile.FullName);
            yield return loadContext.LoadFromAssemblyPath(pluginFile.FullName);
         }
      }

      private IEnumerable<IPlugin> CreateCommands(IEnumerable<Assembly> assemblies)
      {
         int count = 0;
         foreach (var assembly in assemblies)
         {
            _logger.LogInformation($"Loading commands from: {assembly} from {assembly.Location}.");
            try
            {
               assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
               _logger.LogError(ex.Message);
               continue;
            }
            foreach (Type type in assembly.GetTypes())
            {
               _logger.LogDebug("Trying to create instance of {type}", type.FullName);
               if (typeof(IPlugin).IsAssignableFrom(type))
               {
                  var result = Activator.CreateInstance(type) as IPlugin;
                  if (result != null)
                  {
                     // assign logger to plugin
                     result.Logger = _logger;

                     count++;
                     yield return result;
                  }
               }
            }

            if (count == 0)
            {
               _logger.LogInformation($"No commands found in {assembly} from {assembly.Location}. Loading assembly as resource library.");
               // load dll, which has been placed here by plugins
               Assembly.LoadFrom(assembly.Location);
            }
         }
      }
   }
}