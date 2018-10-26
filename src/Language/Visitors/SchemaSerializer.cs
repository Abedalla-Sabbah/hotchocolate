using System;
using System.Linq;
using System.Text;

namespace HotChocolate.Language
{
    public class SchemaSerializer
        : SchemaSyntaxWalker
    {
        private readonly StringBuilder _result = new StringBuilder();
        private DocumentWriter _writer;
        private bool _indent = false;

        public SchemaSerializer()
        {
        }

        public SchemaSerializer(bool useIndentation)
        {
            _indent = useIndentation;
        }

        public string Value => _result.ToString();

        public override void Visit(DocumentNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            _result.Clear();
            using (_writer = new DocumentWriter(_result))
            {
                VisitDocument(node);
                _writer.Flush();
            }
        }

        protected override void VisitDocument(DocumentNode node)
        {
            if (node.Definitions.Any())
            {
                VisitDefinition(node.Definitions.First());

                foreach (IDefinitionNode item in node.Definitions.Skip(1))
                {
                    if (_indent)
                    {
                        _writer.WriteLine();
                        _writer.WriteLine();
                    }
                    else
                    {
                        _writer.WriteSpace();
                    }

                    VisitDefinition(item);
                }
            }
        }

        protected override void VisitObjectTypeDefinition(
            ObjectTypeDefinitionNode node)
        {
            _writer.Write(Keywords.Type);
            _writer.WriteSpace();
            VisitName(node.Name);

            if (node.Directives.Any())
            {
                _writer.WriteSpace();
                _writer.WriteMany(node.Directives, VisitDirective, " ");
            }

            WriteLeftBrace();

            _writer.Indent();
            _writer.WriteMany(
                node.Fields,
                VisitFieldDefinition,
                WriteLineOrSpace);
            _writer.Unindent();

            WriteRightBrace();
        }

        protected override void VisitFieldDefinition(FieldDefinitionNode node)
        {
            _writer.WriteIndentation();

            VisitName(node.Name);

            if (node.Arguments.Any())
            {
                _writer.Write("(");
                _writer.WriteMany(
                    node.Arguments,
                    VisitInputValueDefinition,
                    ", ");
                _writer.Write(")");
            }

            _writer.Write(":");
            _writer.WriteSpace();

            VisitType(node.Type);
        }

        protected override void VisitInputValueDefinition(
            InputValueDefinitionNode node)
        {
            VisitName(node.Name);
            _writer.Write(":");
            _writer.WriteSpace();

            VisitType(node.Type);

            if (!node.DefaultValue.IsNull())
            {
                _writer.WriteSpace();
                _writer.Write("=");
                _writer.WriteSpace();
                VisitValue(node.DefaultValue);
            }
        }

        protected override void VisitIntValue(IntValueNode node)
        {
            _writer.Write(node.Value);
        }

        protected override void VisitFloatValue(FloatValueNode node)
        {
            _writer.Write(node.Value);
        }

        protected override void VisitStringValue(StringValueNode node)
        {
            if (node.Block)
            {
                _writer.Write("\"\"\"");

                string[] lines = node.Value
                    .Replace("\"\"\"", "\\\"\"\"")
                    .Replace("\r", string.Empty)
                    .Split('\n');

                foreach (string line in lines)
                {
                    _writer.WriteLine();
                    _writer.WriteIndentation();
                    _writer.Write(line);
                }

                _writer.WriteLine();
                _writer.WriteIndentation();
                _writer.Write("\"\"\"");
            }
            else
            {
                _writer.Write($"\"{node.Value}\"");
            }
        }

        protected override void VisitBooleanValue(BooleanValueNode node)
        {
            _writer.Write(node.Value.ToString().ToLowerInvariant());
        }

        protected override void VisitEnumValue(EnumValueNode node)
        {
            _writer.Write(node.Value);
        }

        protected override void VisitNullValue(NullValueNode node)
        {
            _writer.Write("null");
        }

        protected override void VisitListValue(ListValueNode node)
        {
            _writer.Write("[ ");

            _writer.WriteMany(node.Items, VisitValue);

            _writer.Write(" ]");
        }

        protected override void VisitObjectValue(ObjectValueNode node)
        {
            _writer.Write("{ ");

            _writer.WriteMany(node.Fields, VisitObjectField);

            _writer.Write(" }");
        }

        protected override void VisitObjectField(ObjectFieldNode node)
        {
            VisitName(node.Name);

            _writer.Write(": ");

            VisitValue(node.Value);
        }

        protected override void VisitVariable(VariableNode node)
        {
            _writer.Write('$');
            VisitName(node.Name);
        }

        protected override void VisitDirective(DirectiveNode node)
        {
            _writer.Write('@');

            VisitName(node.Name);

            if (node.Arguments.Any())
            {
                _writer.Write('(');

                _writer.WriteMany(node.Arguments, VisitArgument);

                _writer.Write(')');
            }
        }

        protected override void VisitArgument(ArgumentNode node)
        {
            VisitName(node.Name);

            _writer.Write(": ");

            VisitValue(node.Value);
        }

        protected override void VisitNonNullType(NonNullTypeNode node)
        {
            VisitType(node.Type);
            _writer.Write('!');
        }

        protected override void VisitListType(ListTypeNode node)
        {
            _writer.Write('[');
            VisitType(node.Type);
            _writer.Write(']');
        }

        protected override void VisitNamedType(NamedTypeNode node)
        {
            VisitName(node.Name);
        }

        protected override void VisitName(NameNode node)
        {
            _writer.Write(node.Value);
        }

        private void WriteLeftBrace()
        {
            _writer.WriteSpace();
            _writer.Write("{");
            WriteLineOrSpace();
        }

        private void WriteRightBrace()
        {
            WriteLineOrSpace();
            _writer.Write("}");
        }

        private void WriteLineOrSpace()
        {
            if (_indent)
            {
                _writer.WriteLine();
            }
            else
            {
                _writer.WriteSpace();
            }
        }
    }
}
