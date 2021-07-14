// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
            : this(start, message, null)
        {
        }

        public TyeYamlException(Mark start, string message, Exception? innerException)
            : base($"Error parsing YAML: ({start.Line}, {start.Column}): {message}", innerException)
        {
        }

        public TyeYamlException(Mark start, string message, Exception? innerException, FileInfo fileInfo)
            : base($"Error parsing '{fileInfo.Name}': ({start.Line}, {start.Column}): {message}", innerException)
        {
        }

        public TyeYamlException(string message, Exception? inner)
            : base(message, inner)
        {
        }
    }
}
