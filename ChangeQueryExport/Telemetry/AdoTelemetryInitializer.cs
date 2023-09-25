using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace AdoQueries.Telemetry
{
    internal class AdoTelemetryInitializer : ITelemetryInitializer
   {
      public void Initialize(ITelemetry telemetry)
      {
         // Replace with actual properties.
         (telemetry as ISupportProperties).Properties["Project"] = "Azure DevOps Query Executor with Plugins";
      }
   }
}