using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace PluginBase
{
    public interface IPlugin
    {
        string Name { get; }
        string Description { get; }

        int Execute(List<IReportItem> items);

        ILogger Logger { get; set; }
    }
}