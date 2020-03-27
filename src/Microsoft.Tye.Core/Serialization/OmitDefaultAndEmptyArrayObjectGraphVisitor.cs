// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace Microsoft.Tye.Serialization
{
    internal sealed class OmitDefaultAndEmptyArrayObjectGraphVisitor
        : ChainedObjectGraphVisitor
    {
        public OmitDefaultAndEmptyArrayObjectGraphVisitor(IObjectGraphVisitor<IEmitter> nextVisitor)
           : base(nextVisitor) { }

        private static object? GetDefault(Type type)
        {
            return type.GetTypeInfo().IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static bool IsEmptyArray(Type type, object? value)
        {
            return value is object
                && value is ICollection
                && ((ICollection)value).Count == 0;
        }

        public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context)
        {
            var defaultValueAttribute = key.GetCustomAttribute<DefaultValueAttribute>();

            var defaultValue = defaultValueAttribute != null
                ? defaultValueAttribute.Value
                : GetDefault(key.Type);

            return !Equals(value.Value, defaultValue)
                   && !IsEmptyArray(value.Type,value.Value)
                   && base.EnterMapping(key, value, context);
        }
    }
}
