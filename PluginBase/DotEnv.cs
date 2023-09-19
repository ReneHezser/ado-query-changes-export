using System;
using System.IO;

namespace PluginBase
{
   public static class DotEnv
   {
      public static void Load(string filePath)
      {
         if (string.IsNullOrEmpty(filePath))
            return;

         if (!File.Exists(filePath))
         {
            throw new FileNotFoundException($"File not found: {filePath}");
         }

         foreach (var line in File.ReadAllLines(filePath))
         {
            var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
               continue;

            Environment.SetEnvironmentVariable(parts[0], parts[1]);
         }
      }
   }
}