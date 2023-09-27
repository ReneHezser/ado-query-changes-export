using AdoQueries;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;

class WorkItemManager
{
   private WorkItemTrackingHttpClient httpClient;
   private readonly ILogger<Worker> logger;

   public WorkItemManager(WorkItemTrackingHttpClient httpClient, ILogger<Worker> logger)
   {
      this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
   }

   // holds all fetched WorkItems to not query multiple times
   public IList<WorkItem> workItems { get; private set; } = new List<WorkItem>();
   // holds all fetched WorkItemLinks to not query multiple times
   public IList<WorkItemLink> workItemLinks { get; private set; } = new List<WorkItemLink>();
   public IDictionary<int, List<WorkItem>> workItemRevisions { get; private set; } = new Dictionary<int, List<WorkItem>>();

   public void Add(WorkItem workItem)
   {
      if (!workItems.Contains(workItem))
         workItems.Add(workItem);
   }
   public void Add(WorkItemLink workItemLink)
   {
      if (!workItemLinks.Contains(workItemLink))
         workItemLinks.Add(workItemLink);
   }
   public void AddRange(IEnumerable<WorkItem> workItems)
   {
      workItems.ForEach(Add);
   }
   public void AddRange(IEnumerable<WorkItemLink> workItemLinks)
   {
      workItemLinks.ForEach(Add);
   }

   internal async Task<IEnumerable<WorkItem>> GetWorkItemsAsync(IList<int> ids, DateTime asOf, WorkItemExpand expand)
   {
      using (logger.BeginScope("WorkItemManager.GetWorkItemsAsync ({ids.Length} items)"))
      {
         // query only items that have not been queried before
         var idsToQuery = ids.Where(id => !workItems.Any(wi => wi.Id == id));

         List<WorkItem> newWorkItems = httpClient.GetWorkItemsAsync(idsToQuery, null, asOf, expand).Result;
         newWorkItems.ForEach(Add);

         return workItems.Where(wi => wi.Id != null && ids.Contains(wi.Id.Value));
      }
   }

   /// <summary>
   /// get all revisions of a workitem. Does internal paging to get all revisions
   /// </summary>
   /// <param name="id"></param>
   /// <param name="expand"></param>
   /// <param name="top"></param>
   /// <param name="skip"></param>
   /// <returns></returns>
   internal async Task<List<WorkItem>> GetRevisionsAsync(int id, WorkItemExpand expand, int? top = 200, int? skip = 0)
   {
      using (logger.BeginScope("WorkItemManager.GetRevisionsAsync"))
      {
         if (!workItemRevisions.ContainsKey(id))
         {
            // might need to query multiple times to get all revisions. Otherwise the page size is limited to 200
            List<WorkItem> revisions;
            var allRevisions = new List<WorkItem>();
            do
            {
               revisions = await httpClient.GetRevisionsAsync(id, top, skip, expand).ConfigureAwait(false);
               allRevisions.AddRange(revisions);
               skip += top;
            } while (revisions.Count > 0);

            workItemRevisions.Add(id, allRevisions);
         }
         return workItemRevisions[id];
      }
   }

   internal async Task<WorkItemQueryResult> QueryByWiqlAsync(Wiql wiql)
   {
      // TODO implement batching
      using (logger.BeginScope("WorkItemManager.QueryByWiqlAsync"))
      {
         WorkItemQueryResult result = await httpClient.QueryByWiqlAsync(wiql).ConfigureAwait(false);
         AddRange(result.WorkItemRelations);
         return result;
      }
   }
}