using System.Collections.Generic;

namespace PluginBase
{
   public interface IReportItem
   {
      public int ID { get; set; }
      public int VersionID { get; set; }
      public string Title { get; set; }
      /// <summary>
      /// All fields from the latest version of the item
      /// </summary>
      public IDictionary<string, object> CurrentItemFields { get; set; }
      public List<IChangedField> ChangedFields { get; set; }
      public string LinkToItem { get; set; }
      public string LinkToParent { get; set; }
      public string EngineeringWorkItemURL { get; set; }

      public static string[] IgnoreFields { get; set; }
      /// <summary>
      /// Ignore versions, that have been changed by these users.
      /// </summary>
      public static string[] IgnoreChangedBy { get; set; }

   }
}