using System.Diagnostics;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using PluginBase;

namespace AdoQueries
{
    public class QueryExecutor
    {
        private readonly Uri uri;
        private readonly string project;
        private readonly string personalAccessToken;
        private readonly int lastChangedWithinDays;

        /// <summary>
        ///     Initializes a new instance of the <see cref="QueryExecutor" /> class.
        /// </summary>
        /// <param name="orgName">
        ///     An organization in Azure DevOps Services. If you don't have one, you can create one for free:
        ///     <see href="https://go.microsoft.com/fwlink/?LinkId=307137" />.
        /// </param>
        /// <param name="personalAccessToken">
        ///     A Personal Access Token, find out how to create one:
        ///     <see href="/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops" />.
        /// </param>
        public QueryExecutor(string orgName, string project, string personalAccessToken, int lastChangedWithinDays)
        {
            uri = new Uri("https://dev.azure.com/" + orgName);
            this.project = project;
            this.personalAccessToken = personalAccessToken;
            this.lastChangedWithinDays = lastChangedWithinDays;
        }

        private static string ReadAdoQuery(string fileName)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"File not found: {fileName}");
            }

            return File.ReadAllText(fileName);
        }

        public async Task<List<IReportItem>> QueryWorkitems()
        {
            List<IReportItem> reportItems = new List<IReportItem>();
            var credentials = new VssBasicCredential(string.Empty, personalAccessToken);

            // create a wiql object and build our query
            var wiql = new Wiql()
            {
                Query = CleanQuery(string.Format(ReadAdoQuery("ado-query.wiql"), project))
            };

            // create instance of work item tracking http client
            using (var httpClient = new WorkItemTrackingHttpClient(uri, credentials))
            {
                var workItemManager = new WorkItemManager(httpClient);
                WorkItemQueryResult result = await workItemManager.QueryByWiqlAsync(wiql).ConfigureAwait(false);
                IList<WorkItemLink> workItemRelations = result.WorkItemRelations.ToList();

                var ids = workItemRelations.Select(item => item.Target.Id).ToArray();
                // some error handling
                if (ids.Length == 0) return reportItems;

                // ignore Feedback items and sort from newest to oldest
                foreach (var relation in workItemRelations
                    .Where(wir => !string.IsNullOrEmpty(wir.Rel))
                    .OrderByDescending(wir => wir.Rel))
                {
                    GetWorkitemChanges(workItemManager, relation, result, reportItems);
                }

                return reportItems;
            }
        }

        private void GetWorkitemChanges(WorkItemManager workItemManager, WorkItemLink relation, WorkItemQueryResult result, List<IReportItem> reportItems)
        {
            WorkItem workItem = workItemManager.GetWorkItemAsync(relation.Target.Id, result.AsOf, WorkItemExpand.Fields).Result;
            var changedDate = Extensions.GetFieldValue<DateTime>(workItem.Fields["System.ChangedDate"]);
            if (changedDate <= DateTime.Now.AddDays(-lastChangedWithinDays)) return;

            // Feature has been changed in the last x days. start with the latest version
            List<WorkItem> revisions = workItemManager.GetRevisionsAsync(workItem.Id.Value, WorkItemExpand.Fields).Result.OrderByDescending(wi => wi.Rev).ToList();
            int versionCount = revisions.Count();
            int versionIndex = 0;
            var currentItem = revisions[0];
            var previousItem = revisions[1];

            // check all revisions that fall into the desired timeframe
            while (Extensions.GetFieldValue<DateTime>(currentItem.Fields["System.ChangedDate"]) >= DateTime.Now.AddDays(-lastChangedWithinDays))
            {
                List<IChangedField> changes = ReportItem.GetChangedFields(previousItem, currentItem, revisions);
                if (changes.Any())
                {
                    // store changes for the WorkItem revision
                    if (reportItems.Any(ri => ri.ID == workItem.Id.Value))
                    {
                        // already added
                        var reportItem = reportItems.First(ri => ri.ID == workItem.Id.Value);
                        reportItem.ChangedFields.AddRange(changes);
                    }
                    else
                    {
                        int length = workItem.Url.IndexOf("/revisions/");
                        if (length == -1) throw new ArgumentException(@"WorkItem.Url '{workItem.Url}' does not contain '/revisions/'");
                        var linkToItem = workItem.Url.Substring(0, length);
                        reportItems.Add(new ReportItem
                        {
                            ID = workItem.Id.Value,
                            VersionID = workItem.Rev.Value,
                            Title = Extensions.GetFieldValue<string>(workItem.Fields["System.Title"]),
                            LinkToItem = linkToItem,
                            LinkToParent = relation.Source.Url,
                            ChangedFields = changes
                        });
                    }
                }

                // prepare the next versions
                versionIndex++;
                if (versionIndex >= versionCount - 1) break;
                
                currentItem = revisions[versionIndex];
                previousItem = revisions[versionIndex + 1];
            }
        }

        /// <summary>
        /// remove line breaks and multiple spaces
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private string CleanQuery(string query)
        {
            var cleanQuery = query.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Replace("  ", " ").Trim();
            Trace.WriteLine(cleanQuery);
            return cleanQuery;
        }
    }
}
