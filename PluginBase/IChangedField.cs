namespace PluginBase
{
    public interface IChangedField
   {
      public string Key { get; set; }
      public object PreviousValue { get; set; }
      public object CurrentValue { get; set; }
   }
}