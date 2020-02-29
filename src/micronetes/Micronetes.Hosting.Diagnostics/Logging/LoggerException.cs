using System;
using System.Collections;
using System.Runtime.Serialization;
using System.Text.Json;

namespace Micronetes.Hosting.Diagnostics.Logging
{
    internal class LoggerException : Exception
    {
        private readonly JsonElement _exceptionMessage;

        public LoggerException(JsonElement exceptionMessage)
        {
            _exceptionMessage = exceptionMessage;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ClassName", _exceptionMessage.GetProperty("TypeName").GetString(), typeof(string)); // Do not rename (binary serialization)
            info.AddValue("Message", Message, typeof(string)); // Do not rename (binary serialization)
            info.AddValue("Data", Data, typeof(IDictionary)); // Do not rename (binary serialization)
            info.AddValue("InnerException", null, typeof(Exception)); // Do not rename (binary serialization)
            info.AddValue("HelpURL", null, typeof(string)); // Do not rename (binary serialization)
            info.AddValue("StackTraceString", StackTrace, typeof(string)); // Do not rename (binary serialization)
            info.AddValue("RemoteStackTraceString", StackTrace, typeof(string)); // Do not rename (binary serialization)
            info.AddValue("RemoteStackIndex", 0, typeof(int)); // Do not rename (binary serialization)
            info.AddValue("ExceptionMethod", null, typeof(string)); // Do not rename (binary serialization)
            info.AddValue("HResult", int.Parse(_exceptionMessage.GetProperty("HResult").GetString())); // Do not rename (binary serialization)
            info.AddValue("Source", Source, typeof(string)); // Do not rename (binary serialization
            info.AddValue("WatsonBuckets", null, typeof(byte[])); // Do not rename (binary serialization)
        }

        public override string Message => _exceptionMessage.GetProperty("Message").GetString();

        public override string StackTrace => _exceptionMessage.GetProperty("VerboseMessage").GetString();

        public override string ToString()
        {
            return _exceptionMessage.GetProperty("VerboseMessage").GetString();
        }
    }
}
