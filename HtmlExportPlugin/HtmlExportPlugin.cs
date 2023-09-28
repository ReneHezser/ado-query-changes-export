using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace HtmlExportPlugin
{
   public class HtmlExportPlugin : IPlugin
   {
      public string Name { get => "HTML Export Plugin"; }
      public string Description { get => "Exports changes to an HTML file."; }
      public ILogger Logger { get; set; }

      public static string[] IgnoreFieldsStartingWith { get; set; } = new[] {
         "System.BoardColumn", "Microsoft.VSTS.", "WEF_"
      };

      public int Execute(List<IReportItem> items)
      {
         if (Logger is null) throw new ArgumentNullException(nameof(Logger));

         CreateHtml(items);
         return 0;
      }

      internal void CreateHtml(List<IReportItem> workItems)
      {
         string source;
         try
         {
            var templatePath = Path.Combine(new[] { "Plugins", "handlebars-template.js" });
            source = File.ReadAllText(templatePath);
         }
         catch (FileNotFoundException)
         {
            Logger.LogInformation("Cannot find the HTML template 'handlebars-template.js' in the Plugins folder. Please make sure it is there and try again.");
            return;
         }

         string filename = $"{Environment.GetEnvironmentVariable("ORGANIZATION")}-{Environment.GetEnvironmentVariable("PROJECT")}-{DateTime.Now.ToShortDateString().Replace("/", "-")}-Export";

         Handlebars.RegisterHelper("StringEqualityBlockHelper", (output, options, context, arguments) =>
         {
            if (arguments.Length != 2)
            {
               throw new HandlebarsException("{{#StringEqualityBlockHelper}} helper must have exactly two arguments");
            }

            var left = arguments.At<string>(0);
            var right = arguments[1] as string;
            if (left == right) options.Template(output, context);
            else options.Inverse(output, context);
         });

         var template = Handlebars.Compile(source);
         var data = new
         {
            title = filename,
            ReportItems = FilterItems(workItems)
         };
         var result = template(data);
         File.WriteAllText(filename + ".html", result);
         Logger.LogInformation($"HTML file written to {filename}.html");
      }

      /// <summary>
      /// remove all fields that start with the given strings
      /// </summary>
      /// <param name="workItems"></param>
      /// <returns></returns>
      private IReportItem[] FilterItems(List<IReportItem> workItems)
      {
         var result = new List<IReportItem>();
         foreach (var item in workItems.ToArray())
         {
            result.Add(item);
            var remainingFields = new List<IChangedField>();

            foreach (var field in item.ChangedFields)
            {
               if (IgnoreFieldsStartingWith.Any(field.Key.StartsWith))
                  continue;

               remainingFields.Add(field);
            }
            // replace all fields with the 
            item.ChangedFields = remainingFields;
         }
         return workItems.ToArray();
      }
   }
}