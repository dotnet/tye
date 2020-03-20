// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Tye.Hosting.Model
{
    public class ReplicaStatusJson
    {
        public string? Name { get; set; }

        public IEnumerable<int>? Ports { get; set; }

        public static JsonConverter<ReplicaStatus> JsonConverter = new Converter();

        private class Converter : JsonConverter<ReplicaStatus>
        {
            public override ReplicaStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return (ReplicaStatus)JsonSerializer.Deserialize(ref reader, typeToConvert.GetType(), options);
            }

            public override void Write(Utf8JsonWriter writer, ReplicaStatus value, JsonSerializerOptions options)
            {
                // Use the runtime type since we really want to serialize either the DockerStatus or ProcessStatus
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }
    }
}
