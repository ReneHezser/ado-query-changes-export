using PluginBase;
using System;
using System.Collections.Generic;
using System.IO;
using HandlebarsDotNet;

namespace HtmlExportPlugin
{
   public class HtmlExportPlugin : ICommand
   {
      public string Name { get => "HTML Export Plugin"; }
      public string Description { get => "Exports changes to an HTML file."; }

      public int Execute(List<IReportItem> items)
      {
         CreateHtml(items);
         return 0;
      }

      internal static void CreateHtml(List<IReportItem> workItems)
      {
         string source;
         try
         {
            source = File.ReadAllText("Plugins/handlebars-template.js");
         }
         catch (FileNotFoundException)
         {
            Console.WriteLine("Cannot find the HTML template 'handlebars-template.js' in the Plugins folder. Please make sure it is there and try again.");
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
            ReportItems = workItems.ToArray()
         };
         var result = template(data);
         File.WriteAllText(filename + ".html", result);
         Console.WriteLine($"HTML file written to {filename}.html");
      }
   }
}