// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tye
{
    public abstract class Pipeline
    {
        private static readonly Dictionary<Type, List<MulticastDelegate>> callbacks = new Dictionary<Type, List<MulticastDelegate>>();

        public static void Configure<TApplication>(Action<TApplication> callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (!callbacks.TryGetValue(typeof(TApplication), out var delegates))
            {
                delegates = new List<MulticastDelegate>();
                callbacks.Add(typeof(TApplication), delegates);
            }

            delegates.Add(callback);
        }

        public static Task<object?> ExecuteAsync(Pipeline pipeline)
        {
            if (pipeline is null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            foreach (var kvp in callbacks)
            {
                var type = kvp.Key;
                var delegates = kvp.Value;

                var method = pipeline.GetType().GetMethod("Register").MakeGenericMethod(type);
                for (var i = 0; i < delegates.Count; i++)
                {
                    method.Invoke(pipeline, new[] { delegates[i], });
                }
            }

            return pipeline.ExecuteAsync();
        }

        public abstract void Register<TApplication>(Action<TApplication> callback);

        public abstract Task<object?> ExecuteAsync();
    }
}
