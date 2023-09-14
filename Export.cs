using HandlebarsDotNet;


class Export
{
   private static string _filename = $"{Environment.GetEnvironmentVariable("ORGANIZATION")}-{Environment.GetEnvironmentVariable("PROJECT")}-{DateTime.Now.ToShortDateString().Replace("/", "-")}-Export";

   internal static void CreateCsv(List<ReportItem> workItems)
   {
      using (var writer = new StreamWriter(_filename + ".csv"))
      {
         writer.WriteLine("ID,VersionID,Title,Field,OldValue,NewValue");
         foreach (var workItem in workItems)
         {
            foreach (var changedField in workItem.ChangedFields)
            {
               writer.WriteLine($"{workItem.ID},{workItem.VersionID},{workItem.Title},{changedField.Key},\"{changedField.previousValue}\",\"{changedField.currentValue}\"");
            }
         }
      }
   }

   internal static void CreateHtml(List<ReportItem> workItems)
   {
      string source = File.ReadAllText("handlebars-template.js");

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
         title = $"{Environment.GetEnvironmentVariable("ORGANIZATION")}-{Environment.GetEnvironmentVariable("PROJECT")}-{DateTime.Now.ToShortDateString()}-Export",
         ReportItems = workItems.ToArray()
      };
      var result = template(data);
      File.WriteAllText(_filename + ".html", result);
   }
}
