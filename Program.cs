using System.Drawing;

namespace AdoQueries;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Exports changes from an Azure DevOps query to an HTML file");
        DotEnv.Load(".env");
        try
        {
            int queryLastDays = 7;
            if (args.Length > 0)
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
            foreach (var workItem in workItems)
            {
                Console.WriteLine(workItem);
            }

            // Export.CreateCsv(workItems);
            Export.CreateHtml(workItems);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            Console.WriteLine(ex.ToString());
        }
    }
}
