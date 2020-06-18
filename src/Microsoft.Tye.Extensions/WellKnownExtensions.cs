﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Tye.Extensions.Dapr;
using Microsoft.Tye.Extensions.Elastic;
using Microsoft.Tye.Extensions.Seq;
using Microsoft.Tye.Extensions.Zipkin;

namespace Microsoft.Tye.Extensions
{
    public static class WellKnownExtensions
    {
        public static IReadOnlyDictionary<string, Extension> Extensions = new Dictionary<string, Extension>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "dapr", new DaprExtension() },
            { "elastic", new ElasticStackExtension() },
            { "seq", new SeqExtension() },
            { "zipkin", new ZipkinExtension() },
        };
    }
}
