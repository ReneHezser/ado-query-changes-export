using System.Collections.Generic;

namespace PluginBase
{
    public interface IReportItem
   {
      public int ID { get; set; }
      public int VersionID { get; set; }
      public string Title { get; set; }
      public List<IChangedField> ChangedFields { get; set; }
      public string LinkToItem { get; set; }
      public string LinkToParent { get; set; }

      public static string[] IgnoreFields { get; set; }
   }
}