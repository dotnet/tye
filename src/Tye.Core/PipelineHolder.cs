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
