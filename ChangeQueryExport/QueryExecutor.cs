using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using PluginBase;

namespace AdoQueries
{
    public class QueryExecutor
    {
        private readonly Uri uri;
        private readonly ILogger<Worker> logger;
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
        public QueryExecutor(ILogger<Worker> logger, string? orgName, string? project, string? personalAccessToken, int lastChangedWithinDays)
        {
            if (string.IsNullOrEmpty(orgName)) throw new ArgumentException($"'{nameof(orgName)}' cannot be null or empty.", nameof(orgName));
            if (string.IsNullOrEmpty(project)) throw new ArgumentException($"'{nameof(project)}' cannot be null or empty.", nameof(project));
            if (string.IsNullOrEmpty(personalAccessToken)) throw new ArgumentException($"'{nameof(personalAccessToken)}' cannot be null or empty.", nameof(personalAccessToken));

            uri = new Uri("https://dev.azure.com/" + orgName);
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            this.personalAccessToken = personalAccessToken ?? throw new ArgumentNullException(nameof(personalAccessToken));
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
                var workItemManager = new WorkItemManager(httpClient, logger);
                WorkItemQueryResult result = await workItemManager.QueryByWiqlAsync(wiql).ConfigureAwait(false);
                IList<WorkItemLink> workItemRelations = result.WorkItemRelations.ToList();

                var ids = workItemRelations.Select(item => item.Target.Id).ToArray();
                // some error handling
                if (ids.Length == 0) return reportItems;

                // ignore Feedback items and sort from newest to oldest
                var workItemRelationIds = workItemRelations
                    .Where(wir => !string.IsNullOrEmpty(wir.Rel))
                    .OrderByDescending(wir => wir.Rel)
                    .Select(wir => wir.Target.Id).ToList();

                // Split into groups of 200 items
                var QueryGroups = from i in Enumerable.Range(0, workItemRelationIds.Count())
                                  group workItemRelationIds[i] by i / 200;
                List<WorkItem> workItems = new List<WorkItem>();
                foreach (var queryGroup in QueryGroups)
                {
                    if (queryGroup.Count() != 0)
                    {
                        // query group and add to workitems
                        var groupIds = queryGroup.Select(i => i).ToArray();
                        workItems.AddRange(workItemManager.GetWorkItemsAsync(groupIds, result.AsOf, WorkItemExpand.Fields).Result);
                    }
                }

                // start with the newest version
                foreach (var workItem in workItems.OrderByDescending(wi => wi.Rev))
                {
                    var workItemRelation = workItemRelations.First(wir => wir.Target.Id == workItem.Id);
                    GetWorkitemChanges(workItemManager, workItem, workItemRelation, reportItems);
                }

                return reportItems;
            }
        }

        private void GetWorkitemChanges(WorkItemManager workItemManager, WorkItem workItem, WorkItemLink workItemLink, List<IReportItem> reportItems)
        {
            if (workItem.Id == null) throw new ArgumentException("WorkItem.Id is null");
            if (workItem.Rev == null) throw new ArgumentException("WorkItem.Rev is null");
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
                var engineeringWorkItemURL = workItem.Fields.ContainsKey("Custom.EngineeringWorkItemURL") ? Extensions.GetFieldValue<string>(workItem.Fields["Custom.EngineeringWorkItemURL"]) : null;
                List<IChangedField> changes = ReportItem.GetChangedFields(previousItem, currentItem, revisions);
                if (changes.Any())
                {
                    // store changes for the WorkItem revision
                    if (reportItems.Any(ri => ri.ID == workItem.Id.Value))
                    {
                        // already added
                        var reportItem = reportItems.First(ri => ri.ID == workItem.Id.Value);
                        if (!string.IsNullOrEmpty(engineeringWorkItemURL)) reportItem.EngineeringWorkItemURL = engineeringWorkItemURL;
                        reportItem.ChangedFields.AddRange(changes);
                    }
                    else
                    {
                        int length = workItem.Url.IndexOf("/revisions/");
                        if (length == -1) throw new ArgumentException(@"WorkItem.Url '{workItem.Url}' does not contain '/revisions/'");
                        var linkToItem = workItem.Url.Substring(0, length);
                        var reportItem = new ReportItem
                        {
                            ID = workItem.Id.Value,
                            VersionID = workItem.Rev ?? 0,
                            Title = Extensions.GetFieldValue<string>(workItem.Fields["System.Title"]),
                            LinkToItem = linkToItem,
                            LinkToParent = workItemLink.Source.Url,
                            ChangedFields = changes,
                            // add all fields from the item (latest version) in case a plugin wants them
                            CurrentItemFields = workItem.Fields
                        };
                        if (!string.IsNullOrEmpty(engineeringWorkItemURL)) reportItem.EngineeringWorkItemURL = engineeringWorkItemURL;
                        reportItems.Add(reportItem);
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
