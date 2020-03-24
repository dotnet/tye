using System.CommandLine;
using System.CommandLine.IO;
using Serilog.Core;
using Serilog.Events;
using Xunit.Abstractions;

namespace E2ETest
{
    public class TestOutputLogEventSink : ILogEventSink, IConsole, IStandardStreamWriter
    {
        private readonly ITestOutputHelper output;

        public TestOutputLogEventSink(ITestOutputHelper output)
        {
            this.output = output;
        }

        public IStandardStreamWriter Out => this;

        public bool IsOutputRedirected => false;

        public IStandardStreamWriter Error => this;

        public bool IsErrorRedirected => false;

        public bool IsInputRedirected => false;

        public void Emit(LogEvent logEvent)
        {
            output.WriteLine(logEvent.RenderMessage());
        }

        public void Write(string value)
        {
            // our usage of IConsole includes newlines, so strip them out.
            output.WriteLine(value.TrimEnd('\r', '\n'));
        }
    }
}
