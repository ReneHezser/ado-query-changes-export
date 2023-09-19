using PluginBase;
using System;
using System.Collections.Generic;

namespace SamplePlugin
{
    public class SamplePlugin : ICommand
    {
        public string Name { get => "Sample Plugin"; }
        public string Description { get => "Displays hello message."; }

        public int Execute(List<IReportItem> items)
        {
            Console.WriteLine($"Hello from Sample Plugin! I have {items.Count} items to work on.");
            return 0;
        }
    }
}