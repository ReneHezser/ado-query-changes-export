using System.Diagnostics;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;

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
        public QueryExecutor(string orgName, string project, string personalAccessToken, int lastChangedWithinDays = 7)
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

        public async Task<List<ReportItem>> QueryWorkitems()
        {
            var reportItems = new List<ReportItem>();
            var credentials = new VssBasicCredential(string.Empty, personalAccessToken);

            // create a wiql object and build our query
            var wiql = new Wiql()
            {
                Query = CleanQuery(string.Format(ReadAdoQuery("ado-query.wiql"), project))
            };

            // create instance of work item tracking http client
            using (var httpClient = new WorkItemTrackingHttpClient(uri, credentials))
            {
                WorkItemManager workItemManager = new WorkItemManager(httpClient);
                WorkItemQueryResult result = await workItemManager.QueryByWiqlAsync(wiql).ConfigureAwait(false);
                IList<WorkItemLink> workItemRelations = result.WorkItemRelations.ToList();
                var resultCount = workItemRelations.Count();
                var ids = workItemRelations.Select(item => item.Target.Id).ToArray();
                // some error handling
                if (ids.Length == 0) return reportItems;

                // // TODO improve by batching
                // WorkItemBatchGetRequest request = new WorkItemBatchGetRequest { Ids = ids };
                // httpClient.GetWorkItemsBatchAsync(request).ContinueWith(x =>
                // {
                //     foreach (var item in x.Result)
                //     {
                //         hierarchyItems.Add(item.Id.Value, item);
                //     }
                // }).Wait();

                // ignore Feedback items and sort from newest to oldest
                foreach (var relation in workItemRelations
                    .Where(wir => !string.IsNullOrEmpty(wir.Rel))
                    .OrderByDescending(wir => wir.Rel))
                {
                    // // DEBUG
                    // if (relation.Target.Id != 112819) continue;
                    // // END DEBUG
                    WorkItem workItem = workItemManager.GetWorkItemAsync(relation.Target.Id, result.AsOf, WorkItemExpand.Fields).Result;
                    var changedDate = (DateTime)ReportItem.GetFieldValue(workItem.Fields["System.ChangedDate"], typeof(DateTime));
                    if (changedDate <= DateTime.Now.AddDays(-lastChangedWithinDays)) continue;

                    // Feature has been changed in the last x days
                    // start with the latest version
                    List<WorkItem> revisions = workItemManager.GetRevisionsAsync(workItem.Id.Value, WorkItemExpand.Fields, null, null).Result.OrderByDescending(wi => wi.Rev).ToList();
                    int versionCount = revisions.Count();
                    int versionIndex = 0;
                    var currentItem = revisions[0];
                    var previousItem = revisions[1];

                    // check all revisions that fall into the desired timeframe
                    while ((DateTime)ReportItem.GetFieldValue(currentItem.Fields["System.ChangedDate"], typeof(DateTime)) >= DateTime.Now.AddDays(-lastChangedWithinDays))
                    {
                        List<dynamic> changes = ReportItem.GetChangedFields(previousItem, currentItem, revisions);
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
                                var linkToItem = workItem.Url.Substring(0, workItem.Url.IndexOf("/revisions/"));
                                reportItems.Add(new ReportItem
                                {
                                    ID = workItem.Id.Value,
                                    VersionID = workItem.Rev.Value,
                                    Title = (string)ReportItem.GetFieldValue(workItem.Fields["System.Title"]),
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

                return reportItems;
            }
        }

        private string CleanQuery(string query)
        {
            var cleanQuery = query.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Replace("  ", " ").Trim();
            Trace.WriteLine(cleanQuery);
            return cleanQuery;
        }
    }
}
