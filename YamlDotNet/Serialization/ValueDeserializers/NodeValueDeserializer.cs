﻿// This file is part of YamlDotNet - A .NET library for YAML.
// Copyright (c) Antoine Aubry and contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization.Utilities;

namespace YamlDotNet.Serialization.ValueDeserializers
{
    public sealed class NodeValueDeserializer : IValueDeserializer
    {
        private readonly IList<INodeDeserializer> deserializers;
        private readonly IList<INodeTypeResolver> typeResolvers;
        private readonly ITypeConverter typeConverter;

        public NodeValueDeserializer(IList<INodeDeserializer> deserializers, IList<INodeTypeResolver> typeResolvers, ITypeConverter typeConverter)
        {
            this.deserializers = deserializers ?? throw new ArgumentNullException(nameof(deserializers));
            this.typeResolvers = typeResolvers ?? throw new ArgumentNullException(nameof(typeResolvers));
            this.typeConverter = typeConverter ?? throw new ArgumentNullException(nameof(typeConverter));
        }

        public object? DeserializeValue(IParser parser, Type expectedType, SerializerState state, IValueDeserializer nestedObjectDeserializer)
        {
            var nodeEvent = GetNodeEvent(parser, expectedType);
            var nodeType = GetTypeFromEvent(nodeEvent, expectedType);

            try
            {
                foreach (var deserializer in deserializers)
                {
                    if (deserializer.Deserialize(parser, nodeType, (r, t) => nestedObjectDeserializer.DeserializeValue(r, t, state, nestedObjectDeserializer), out var value))
                    {
                        return typeConverter.ChangeType(value, expectedType);
                    }
                }
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new YamlException(
                    nodeEvent?.Start ?? Mark.Empty,
                    nodeEvent?.End ?? Mark.Empty,
                    "Exception during deserialization",
                    ex
                );
            }

            throw new YamlException(
                nodeEvent?.Start ?? Mark.Empty,
                nodeEvent?.End ?? Mark.Empty,
                $"No node deserializer was able to deserialize the node into type {expectedType.AssemblyQualifiedName}"
            );
        }

        private static NodeEvent? GetNodeEvent(IParser parser, Type expectedType)
        {
            parser.Accept<NodeEvent>(out var nodeEvent);
            if (nodeEvent == null
                && !parser.SkipComments
                && !typeof(IYamlConvertible).IsAssignableFrom(expectedType))
            {
                if (parser.Current is YamlDotNet.Core.Events.Comment cmt && cmt.IsInline)
                {
                    return nodeEvent;
                }
                parser.SkipFollowingComments();
                parser.Accept<NodeEvent>(out nodeEvent);
            }

            return nodeEvent;
        }

        private Type GetTypeFromEvent(NodeEvent? nodeEvent, Type currentType)
        {
            foreach (var typeResolver in typeResolvers)
            {
                if (typeResolver.Resolve(nodeEvent, ref currentType))
                {
                    break;
                }
            }
            return currentType;
        }
    }
}
