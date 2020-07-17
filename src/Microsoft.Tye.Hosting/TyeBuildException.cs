// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Tye.Hosting
{
    [Serializable]
    internal class TyeBuildException : Exception
    {
        public TyeBuildException()
        {
        }

        public TyeBuildException(string? message) : base(message)
        {
        }

        public TyeBuildException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected TyeBuildException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
