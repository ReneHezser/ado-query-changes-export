using PluginBase;
using System.Reflection;

namespace AdoQueries
{
    class Program
    {
        private static string version = "1.0.0.1";

        static void Main(string[] args)
        {
            Console.WriteLine("Find changes from an Azure DevOps query and pass them to extensions.");
            Console.WriteLine("Version: " + version);
            try
            {
                var commands = LoadAndExecutePlugins(args ?? new string[0]);
                foreach (ICommand command in commands)
                {
                    Console.WriteLine($"Found plugin {command.Name} - {command.Description}");
                }

                DotEnv.Load(".env");
                int queryLastDays = 7;
                if ((args ?? new string[0]).Length > 0)
                {
                    queryLastDays = int.Parse(args[0]);
                }

                var queryExecutor = new QueryExecutor(
                    Environment.GetEnvironmentVariable("ORGANIZATION"),
                    Environment.GetEnvironmentVariable("PROJECT"),
                    Environment.GetEnvironmentVariable("PERSONAL_ACCESS_TOKEN"),
                    queryLastDays
                    );
                var task = queryExecutor.QueryWorkitems();
                task.Wait();
                var workItems = task.Result;

                foreach (ICommand command in commands)
                {
                    Console.WriteLine($"Executing {command.Name} - {command.Description}");
                    command.Execute(workItems);
                }
                Console.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine(ex.ToString());
            }
        }

        private static IEnumerable<ICommand> LoadAndExecutePlugins(string[] args)
        {
            // Paths to plugins to load.
            string[] pluginPaths = new string[]
            {
                // Directory.GetCurrentDirectory(),
                Directory.GetCurrentDirectory() + "\\Plugins"
            };

            IEnumerable<ICommand> commands = pluginPaths.SelectMany(pluginPath =>
            {
                IEnumerable<Assembly> pluginAssemblies = LoadPlugin(pluginPath);
                return CreateCommands(pluginAssemblies);
            }).ToList();

            return commands;
        }

        static IEnumerable<Assembly> LoadPlugin(string pluginLocation)
        {
            Console.WriteLine($"Loading commands from: {pluginLocation}");
            var loadContext = new PluginLoadContext(pluginLocation);

            FileInfo[] pluginFiles = new DirectoryInfo(pluginLocation).GetFiles("*.dll");

            foreach (var pluginFile in pluginFiles)
            {
                yield return loadContext.LoadFromAssemblyPath(pluginFile.FullName);// LoadFromAssemblyName(new AssemblyName(pluginFilename.Name));
            }
        }

        static IEnumerable<ICommand> CreateCommands(IEnumerable<Assembly> assemblies)
        {
            int count = 0;
            foreach (var assembly in assemblies)
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (typeof(ICommand).IsAssignableFrom(type))
                    {
                        ICommand result = Activator.CreateInstance(type) as ICommand;
                        if (result != null)
                        {
                            count++;
                            yield return result;
                        }
                    }
                }

                if (count == 0)
                {
                    Console.WriteLine($"No commands found in {assembly} from {assembly.Location}. Loading assembly as resource library.");
                    // load dll, which has been placed here by plugins
                    Assembly.LoadFrom(assembly.Location);
                }
            }
        }
    }
}