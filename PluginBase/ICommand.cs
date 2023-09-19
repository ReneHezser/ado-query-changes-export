using System.Collections.Generic;

namespace PluginBase
{
    public interface ICommand
    {
        string Name { get; }
        string Description { get; }

        int Execute(List<IReportItem> items);
    }
}