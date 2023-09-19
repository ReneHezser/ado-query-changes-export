using PluginBase;
using System.Collections;
using System.Text;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;

namespace AdoQueries
{
   public class ReportItem : IReportItem
   {
      public int ID { get; set; }
      public int VersionID { get; set; }
      public string Title { get; set; }
      public List<dynamic> ChangedFields { get; set; } = new List<dynamic>();
      public string LinkToItem { get; set; }
      public string LinkToParent { get; set; }

      public static string[] IgnoreFields { get; set; } = new[] {
      "System.CommentCount",
      "System.AuthorizedDate", "System.RevisedDate",
      "System.AuthorizedAs", "System.PersonId",
      "System.Watermark"
      };

      override public string ToString()
      {
         var sb = new StringBuilder();
         sb.AppendLine($"ID: {ID}, Title: {Title}, ChangedFields:");
         ChangedFields.ForEach(cf => sb.AppendLine($"\t{cf.Key}: {cf.previousValue} -> {cf.currentValue}"));
         return sb.ToString();
      }

      /// <summary>
      /// Get the field value as a string, or other type if specified.
      /// </summary>
      /// <param name="field"></param>
      /// <param name="type"></param>
      /// <returns></returns>
      internal static object GetFieldValue(dynamic field, Type? type = null)
      {
         if (type == null) type = field.GetType();

         if (field == null)
         {
            return string.Empty;
         }

         if (type == typeof(string))
         {
            return field;
         }

         if (type == typeof(DateTime))
         {
            return (DateTime)field;
         }

         if (type == typeof(IEnumerable))
         {
            var values = new List<string>();
            foreach (var value in field)
            {
               values.Add(value.ToString());
            }
            return string.Join(", ", values);
         }

         if (type == typeof(IdentityRef))
         {
            return ((IdentityRef)field).DisplayName;
         }

         return field.ToString();
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="previousItem"></param>
      /// <param name="currentItem"></param>
      /// <param name="allRevisionItems">ordered with the newest being the first</param>
      /// <returns></returns>
      internal static List<dynamic> GetChangedFields(WorkItem previousItem, WorkItem currentItem, List<WorkItem> allRevisionItems)
      {
         if (previousItem is null) throw new ArgumentNullException(nameof(previousItem));
         if (currentItem is null) throw new ArgumentNullException(nameof(currentItem));

         var changedFields = new List<dynamic>();
         foreach (var field in currentItem.Fields)
         {
            if (IgnoreFields.Contains(field.Key)) continue;

            if (!previousItem.Fields.Any(f => f.Key == field.Key))
            {
               // need to get the field value from the item version that are older than the current item
               var olderRevisions = allRevisionItems.Where(r => r.Rev < currentItem.Rev.Value).OrderByDescending(r => r.Rev.Value).ToList();
               for (int i = 0; i < olderRevisions.Count; i++)
               {
                  var olderRevision = olderRevisions[i];
                  if (olderRevision.Fields.Any(f => f.Key == field.Key))
                  {
                     previousItem = olderRevision;
                     break;
                  }
               }
               // Trace.WriteLine(string.Format("ItemID:{0},Field={1},currentRev={2},previousRev={3}", currentItem.Id, field.Key, currentItem.Rev.Value, previousItem.Rev.Value));
            }

            // check for a changed field value. Need to compare the string values, because the field value types are different depending the type of the field
            if (previousItem.Fields.ContainsKey(field.Key) && GetFieldValue(previousItem.Fields[field.Key]).ToString() != GetFieldValue(field.Value).ToString())
            {
               //Trace.WriteLine($"Field {field.Key} has changed from {field.Value} to {currentItem.Fields[field.Key]}");
               changedFields.Add(new
               {
                  Key = field.Key,
                  previousValue = GetFieldValue(previousItem.Fields[field.Key]),
                  currentValue = GetFieldValue(field.Value)
               });
            }
         }

         if (GetFieldValue(currentItem.Fields["System.ChangedBy"]).ToString() == "scrpts")
         {
            // don't add changes by scrpts
            // Trace.WriteLine("Skipping changes by scrpts");
            changedFields.Clear();
         }

         return changedFields;
      }
   }
}