using System;
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
            if (otherNode == null)
            {
                throw new TyeYamlException($"Expected sequence to be not null, difference starting at Expected: ({node.Start.Line}, {node.Start.Column})");
            }

            if (node.Tag?.Equals(otherNode.Tag) == false)
            {
                throw new TyeYamlException($"Expected tags to be equal, difference starting at Expected: ({node.Start.Line}, {node.Start.Column}) Actual: ({otherNode.Start.Line}, {otherNode.Start.Column})");
            }

            if (node.Children.Count != otherNode.Children.Count)
            {
                throw new TyeYamlException($"Number of children differ for sequence, Expected: ({node.Start.Line}, {node.Start.Column}) Actual: ({otherNode.Start.Line}, {otherNode.Start.Column})");
            }

            for (var i = 0; i < node.Children.Count; ++i)
            {
                var childNode = node.Children[i];
                var otherChildNode = otherNode.Children[i];

                if (childNode.NodeType != otherChildNode.NodeType)
                {
                    throw new TyeYamlException($"Child node types differ, Expected: ({childNode.Start.Line}, {childNode.Start.Column}) Actual: ({otherChildNode.Start.Line}, {otherChildNode.Start.Column})");
                }

                if (childNode.NodeType == YamlNodeType.Mapping)
                {
                    VisitMapping((YamlMappingNode)childNode, (YamlMappingNode)otherChildNode);
                }
                else if (childNode.NodeType == YamlNodeType.Scalar)
                {
                    VisitScalar((YamlScalarNode)childNode, (YamlScalarNode)otherChildNode);
                }
                else if (childNode.NodeType == YamlNodeType.Sequence)
                {
                    VisitSequence((YamlSequenceNode)childNode, (YamlSequenceNode)otherChildNode);
                }
            }
        }

        public void VisitMapping(YamlMappingNode node, YamlMappingNode otherNode)
        {
            if (otherNode == null)
            {
                throw new TyeYamlException($"Expected mapping to be not null, difference starting at Expected: ({node.Start.Line}, {node.Start.Column})");
            }

            if (node.Tag?.Equals(otherNode.Tag) == false)
            {
                throw new TyeYamlException($"Expected tags to be equal, difference starting at Expected: ({node.Start.Line}, {node.Start.Column}) Actual: ({otherNode.Start.Line}, {otherNode.Start.Column})");
            }

            if (node.Children.Count != otherNode.Children.Count)
            {
                throw new TyeYamlException($"Number of children differ for mapping, Expected: ({node.Start.Line}, {node.Start.Column}) Actual: ({otherNode.Start.Line}, {otherNode.Start.Column})");
            }

            foreach (var child in node.Children)
            {
                var childValue = child.Value;
                var otherChildValue = otherNode.Children[child.Key];

                if (childValue.NodeType != otherChildValue.NodeType)
                {
                    throw new TyeYamlException($"Child node types differ, Expected: ({childValue.Start.Line}, {childValue.Start.Column}) Actual: ({otherChildValue.Start.Line}, {otherChildValue.Start.Column})");
                }

                if (childValue.NodeType == YamlNodeType.Mapping)
                {
                    VisitMapping((YamlMappingNode)childValue, (YamlMappingNode)otherChildValue);
                }
                else if (childValue.NodeType == YamlNodeType.Scalar)
                {
                    VisitScalar((YamlScalarNode)childValue, (YamlScalarNode)otherChildValue);
                }
                else if (childValue.NodeType == YamlNodeType.Sequence)
                {
                    VisitSequence((YamlSequenceNode)childValue, (YamlSequenceNode)otherChildValue);
                }
            }
        }

        public void VisitScalar(YamlScalarNode node, YamlScalarNode otherNode)
        {
            if (otherNode == null)
            {
                throw new TyeYamlException($"Expected mapping to be not null, difference starting at Expected: ({node.Start.Line}, {node.Start.Column})");
            }

            if (node.Tag?.Equals(otherNode.Tag) == false)
            {
                throw new TyeYamlException($"Expected tags to be equal. " +
                    $"Expected: ({node.Start.Line}, {node.Start.Column}) Actual: ({otherNode.Start.Line}, {otherNode.Start.Column})");
            }

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
