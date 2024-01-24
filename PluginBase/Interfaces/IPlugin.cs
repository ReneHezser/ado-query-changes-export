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

        /// <summary>
        /// Errors that occurred during execution of the plugin will be logged from the application for each plugin.
        /// You can use the key to identify the item that caused the error.
        /// </summary>
        IDictionary<string, string> Errors { get; }
    }
}