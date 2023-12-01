using System.Reflection;
using System.Runtime.Loader;

namespace AdoQueries
{
   class PluginLoadContext : AssemblyLoadContext
   {
      private AssemblyDependencyResolver _resolver;

      public PluginLoadContext(string pluginPath)
      {
         _resolver = new AssemblyDependencyResolver(pluginPath);
      }

      protected override Assembly? Load(AssemblyName assemblyName)
      {
         try
         {
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
               Assembly assembly = LoadFromAssemblyPath(assemblyPath);
               return assembly;
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine($"Cannot load assembly {assemblyName.Name} from plugin path. {ex.Message}");
         }

         return null;
      }

      protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
      {
         string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
         if (libraryPath != null)
         {
            return LoadUnmanagedDllFromPath(libraryPath);
         }

         return IntPtr.Zero;
      }
   }
}