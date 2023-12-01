using PluginBase;
using System.Text;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AdoQueries
{
   public class ReportItem : IReportItem
   {
      public int ID { get; set; }
      public int VersionID { get; set; }
      public string? Title { get; set; }
      public List<IChangedField> ChangedFields { get; set; } = new List<IChangedField>();
      public string? LinkToItem { get; set; }
      public string? LinkToParent { get; set; }
      public string? EngineeringWorkItemURL { get; set; }

      public static string[] IgnoreFields { get; set; } = new[] {
      "System.CommentCount",
      "System.AuthorizedDate", "System.RevisedDate",
      "System.AuthorizedAs", "System.PersonId",
      "System.Watermark"
      };

      public static string[] IgnoreChangedBy { get; set; } = Array.Empty<string>();

      override public string ToString()
      {
         var sb = new StringBuilder();
         sb.AppendLine($"ID: {ID}, Title: {Title}, ChangedFields:");
         ChangedFields.ForEach(cf => sb.AppendLine($"\t{cf.Key}: {cf.PreviousValue} -> {cf.CurrentValue}"));
         return sb.ToString();
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="previousItem"></param>
      /// <param name="currentItem"></param>
      /// <param name="allRevisionItems">ordered with the newest being the first</param>
      /// <returns></returns>
      internal static List<IChangedField> GetChangedFields(WorkItem previousItem, WorkItem currentItem, List<WorkItem> allRevisionItems)
      {
         if (previousItem is null) throw new ArgumentNullException(nameof(previousItem));
         if (currentItem is null) throw new ArgumentNullException(nameof(currentItem));

         var changedFields = new List<IChangedField>();
         foreach (var field in currentItem.Fields)
         {
            if (IgnoreFields.Contains(field.Key)) continue;

            object? previousValue = null;
            // check if the direct previous version has the field
            if (previousItem.Fields.Any(f => f.Key == field.Key))
            {
               // nothing to do, the previous version has the field
               previousValue = Extensions.GetFieldValue<object>(previousItem.Fields[field.Key]);
            }
            else
            {
               // need to get the field value from the item version that are older than the current item
               var olderRevisions = allRevisionItems.Where(r => r.Rev.Value < currentItem.Rev.Value).OrderByDescending(r => r.Rev.Value).ToList();
               for (int i = 0; i < olderRevisions.Count; i++)
               {
                  var olderRevision = olderRevisions[i];
                  if (olderRevision.Fields.Any(f => f.Key == field.Key))
                  {
                     // an older revision has a value. This is the previous value for the changed field
                     previousItem = olderRevision;
                     previousValue = Extensions.GetFieldValue<object>(olderRevision.Fields[field.Key]);
                     break;
                  }
               }
            }

            // check for a changed field value. 
            // if no old version was found to compare against, assume the field value has changed
            // Need to compare the string values, because the field value types are different depending the type of the field
            if (previousItem.Fields.ContainsKey(field.Key) && Extensions.GetFieldValue<string>(previousItem.Fields[field.Key]) != Extensions.GetFieldValue<string>(field.Value))
            {
               previousValue = Extensions.GetFieldValue<object>(previousItem.Fields[field.Key]);
            }

            // now the current and previous value are known. Only add them to the changed fields list, if the value has changed
            if (previousValue != null && (string)previousValue != Extensions.GetFieldValue<string>(field.Value))
            {
               changedFields.Add(new ChangedField
               {
                  Key = field.Key,
                  PreviousValue = previousValue,
                  CurrentValue = Extensions.GetFieldValue<object>(field.Value)
               });
            }
         }

         string changedBy = Extensions.GetFieldValue<string>(currentItem.Fields["System.ChangedBy"]);
         if (IgnoreChangedBy.Any(cb => cb == changedBy))
         {
            // don't add changes by scrpts
            changedFields.Clear();
         }
         // ignore changes in only the Rev field
         if (changedFields.Count == 3 && changedFields.Any(cf => cf.Key == "System.Rev") && changedFields.Any(cf => cf.Key == "System.ChangedDate") && changedFields.Any(cf => cf.Key == "System.ChangedBy"))
         {
            changedFields.Clear();
         }

         return changedFields;
      }
   }
}