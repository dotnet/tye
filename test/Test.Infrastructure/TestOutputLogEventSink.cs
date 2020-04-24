using System.CommandLine;
using System.CommandLine.IO;
using Serilog.Core;
using Serilog.Events;
using Xunit.Abstractions;

namespace Test.Infrastructure
{
    public class TestOutputLogEventSink : ILogEventSink, IConsole, IStandardStreamWriter
    {
        private readonly ITestOutputHelper _output;

        public TestOutputLogEventSink(ITestOutputHelper output)
        {
            _output = output;
        }

        public IStandardStreamWriter Out => this;

        public bool IsOutputRedirected => false;

        public IStandardStreamWriter Error => this;

        public bool IsErrorRedirected => false;

        public bool IsInputRedirected => false;

        public void Emit(LogEvent logEvent)
        {
            _output.WriteLine(logEvent.RenderMessage());
        }

        public void Write(string value)
        {
            // our usage of IConsole includes newlines, so strip them out.
            _output.WriteLine(value.TrimEnd('\r', '\n'));
        }
    }
}
