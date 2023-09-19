using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;

class WorkItemManager
{
   private WorkItemTrackingHttpClient httpClient;

   public WorkItemManager(WorkItemTrackingHttpClient httpClient)
   {
      this.httpClient = httpClient;
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

   /// <summary>
   /// only query for the workitem, if it has not been queried before
   /// </summary>
   /// <param name="id"></param>
   /// <param name="asOf"></param>
   /// <param name="fields"></param>
   /// <returns></returns> <summary>
   /// 
   /// </summary>
   /// <param name="id"></param>
   /// <param name="asOf"></param>
   /// <param name="fields"></param>
   /// <returns></returns>
   internal async Task<WorkItem> GetWorkItemAsync(int id, DateTime asOf, WorkItemExpand expand)
   {
      if (!workItems.Any(wi => wi.Id == id))
      {
         Task<WorkItem> task = httpClient.GetWorkItemAsync(id, null, asOf, expand);
         Add(task.Result);
      }
      return workItems.First(wi => wi.Id == id);
   }

   internal async Task<List<WorkItem>> GetRevisionsAsync(int id, WorkItemExpand expand, int? top = 200, int? skip = 0)
   {
      if (!workItemRevisions.ContainsKey(id))
      {
         List<WorkItem> revisions = await httpClient.GetRevisionsAsync(id, top, skip, expand).ConfigureAwait(false);
         workItemRevisions.Add(id, revisions);
      }
      return workItemRevisions[id];
   }

   internal async Task<WorkItemQueryResult> QueryByWiqlAsync(Wiql wiql)
   {
      WorkItemQueryResult result = await httpClient.QueryByWiqlAsync(wiql).ConfigureAwait(false);
      // AddRange(result.WorkItems);
      AddRange(result.WorkItemRelations);
      return result;
   }
}