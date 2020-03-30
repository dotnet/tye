using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Core;

namespace Tye.Serialization
{
    public class TyeYamlException : Exception
    {
        public TyeYamlException(string message)
            : base(message)
        {
        }

        public TyeYamlException(Mark start, string message)
            : this(start,  message, null)
        {
        }

        public TyeYamlException(Mark start, string message, Exception? innerException)
            : base($"Error parsing tye.yaml: ({start.Line}, {start.Column}): {message}", innerException)
        {
        }

        public TyeYamlException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
