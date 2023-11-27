using System;
using System.IO;

namespace PluginBase
{
   public static class DotEnv
   {
      /// <summary>
      /// read from a file and add the values to the environment
      /// </summary>
      /// <param name="filePath"></param>
      /// <exception cref="FileNotFoundException"></exception>
      public static void Load(string filePath)
      {
         if (string.IsNullOrEmpty(filePath))
            return;

         if (!File.Exists(filePath))
         {
            Console.WriteLine("File not found: {0}", filePath);
            return;
            //throw new FileNotFoundException($"File not found: {filePath}");
         }

         foreach (var line in File.ReadAllLines(filePath))
         {
            var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
               continue;

            Environment.SetEnvironmentVariable(parts[0], parts[1]);
         }
      }
   }
}