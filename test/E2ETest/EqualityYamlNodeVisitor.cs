using System;
using System.Collections.Generic;
using System.Text;
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
            // TODO make this more thorough
            VisitMapping((YamlMappingNode)node, (YamlMappingNode)otherNode);
        }

        public void VisitSequence(YamlSequenceNode node, YamlSequenceNode otherNode)
        {

        }

        public void VisitMapping(YamlMappingNode node, YamlMappingNode otherNode)
        {
            if (node.Children.Count != otherNode.Children.Count)
            {
                throw new Exception();
            }

            foreach (var child in node.Children)
            {
                var childValue = child.Value;
                var otherChildValue = otherNode.Children[child.Key];

                if (childValue.NodeType != otherChildValue.NodeType)
                {
                    throw new Exception();
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

        }
    }
}
