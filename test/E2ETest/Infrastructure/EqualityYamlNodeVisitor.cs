// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using Tye.Serialization;
using YamlDotNet.RepresentationModel;

namespace E2ETest
{
    public class EqualityYamlNodeVisitor
    {
        public EqualityYamlNodeVisitor()
        {
        }

        public void Visit(YamlNode node, YamlNode otherNode)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (otherNode == null)
            {
                throw new ArgumentNullException(nameof(otherNode));
            }

            VisitInternal(node, otherNode);
        }

        private void VisitInternal(YamlNode node, YamlNode otherNode)
        {
            if (node.NodeType != otherNode.NodeType)
            {
                throw new TyeYamlException($"Node types differ, Expected: {node.NodeType} at ({node.Start.Line}, {node.Start.Column}) " +
                    $"Actual: {node.NodeType} at ({otherNode.Start.Line}, {otherNode.Start.Column})");
            }

            if (node.Tag?.Equals(otherNode.Tag) == false)
            {
                throw new TyeYamlException($"Expected tags to be equal. " +
                    $"Expected: ({node.Start.Line}, {node.Start.Column}) Actual: ({otherNode.Start.Line}, {otherNode.Start.Column})");
            }

            if (node.NodeType == YamlNodeType.Mapping)
            {
                VisitMapping((YamlMappingNode)node, (YamlMappingNode)otherNode);
            }
            else if (node.NodeType == YamlNodeType.Scalar)
            {
                VisitScalar((YamlScalarNode)node, (YamlScalarNode)otherNode);
            }
            else if (node.NodeType == YamlNodeType.Sequence)
            {
                VisitSequence((YamlSequenceNode)node, (YamlSequenceNode)otherNode);
            }
        }

        public void VisitSequence(YamlSequenceNode node, YamlSequenceNode otherNode)
        {
            if (node.Children.Count != otherNode.Children.Count)
            {
                throw new TyeYamlException($"Number of children differ for sequence, Expected: ({node.Start.Line}, {node.Start.Column}) Actual: ({otherNode.Start.Line}, {otherNode.Start.Column})");
            }

            for (var i = 0; i < node.Children.Count; ++i)
            {
                var childNode = node.Children[i];
                var otherChildNode = otherNode.Children[i];

                VisitInternal(childNode, otherChildNode);
            }
        }

        public void VisitMapping(YamlMappingNode node, YamlMappingNode otherNode)
        {
            if (node.Children.Count != otherNode.Children.Count)
            {
                throw new TyeYamlException($"Number of children differ for mapping, Expected: ({node.Start.Line}, {node.Start.Column}) Actual: ({otherNode.Start.Line}, {otherNode.Start.Column})");
            }

            foreach (var child in node.Children)
            {
                var childValue = child.Value;

                if (!otherNode.Children.TryGetValue(child.Key, out var otherChildValue))
                {
                    throw new TyeYamlException($"YamlMapping missing node, difference starting at Expected: ({node.Start.Line}, {node.Start.Column}). " +
                                       $"Actual: ({otherNode.Start.Line}, {otherNode.Start.Column})");
                }

                VisitInternal(childValue, otherChildValue);
            }
        }

        public void VisitScalar(YamlScalarNode node, YamlScalarNode otherNode)
        {
            if (!node.Equals(otherNode))
            {
                throw new TyeYamlException($"Scalar nodes have different values." + Environment.NewLine +
                    $"Expected:" + Environment.NewLine +
                    $"{node.Value} at ({node.Start.Line}, {node.Start.Column}). " + Environment.NewLine +
                    $"Actual:" + Environment.NewLine +
                    $"{otherNode.Value} at ({otherNode.Start.Line}, {otherNode.Start.Column}).");
            }
        }
    }
}
