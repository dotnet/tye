using Serilog.Core;
using Serilog.Events;
using Xunit.Abstractions;

namespace E2ETest
{
    public class TestOutputLogEventSink : ILogEventSink
    {
        private readonly ITestOutputHelper output;
        public TestOutputLogEventSink(ITestOutputHelper output)
        {
            this.output = output;
        }

        public void Emit(LogEvent logEvent)
        {
            output.WriteLine(logEvent.RenderMessage());
        }
    }
}
