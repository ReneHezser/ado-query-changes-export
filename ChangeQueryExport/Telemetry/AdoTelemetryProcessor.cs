using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace AdoQueries.Telemetry
{
    internal class AdoTelemetryProcessor : ITelemetryProcessor
   {
      ITelemetryProcessor next;

      public AdoTelemetryProcessor(ITelemetryProcessor next)
      {
         this.next = next;
      }

      public void Process(ITelemetry item)
      {
         // Example processor - not filtering out anything.
         // This should be replaced with actual logic.
         this.next.Process(item);
      }
   }
}