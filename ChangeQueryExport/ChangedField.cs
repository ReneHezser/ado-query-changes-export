using PluginBase;

namespace AdoQueries
{
    public class ChangedField : IChangedField
    {
        public string Key { get; set; }
        public object PreviousValue { get; set; }
        public object CurrentValue { get; set; }
    }
}