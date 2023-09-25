using Microsoft.Extensions.Logging;
using PluginBase;
using System;
using System.Collections.Generic;

namespace SamplePlugin
{
    public class SamplePlugin : IPlugin
    {
        public string Name { get => "Sample Plugin"; }
        public string Description { get => "Displays hello message."; }
        public ILogger Logger { get; set; }

        public int Execute(List<IReportItem> items)
        {
            if (Logger is null) throw new ArgumentNullException(nameof(Logger));
            Logger.LogInformation($"Hello from Sample Plugin! I have {items.Count} items to work on.");
            return 0;
        }
    }
}