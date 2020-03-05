// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Tye
{
    public sealed class PipelineHolder
    {
        public PipelineHolder(Pipeline pipeline)
        {
            if (pipeline is null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            __Pipeline = pipeline;
        }

        public Pipeline __Pipeline { get; }
    }
}
