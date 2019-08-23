using System.Text;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Types;
using StrawberryShake.Generators.Descriptors;
using StrawberryShake.Generators.Utilities;
using static StrawberryShake.Generators.Utilities.NameUtils;

namespace StrawberryShake.Generators.CSharp
{
    public class ResultParserMethodGenerator
    {
        public async Task WriteAsync(
            CodeWriter writer,
            IResultParserMethodDescriptor methodDescriptor,
            ITypeLookup typeLookup)
        {
            string resultTypeName = methodDescriptor.ResultSelection is null
                ? methodDescriptor.ResultSelection.Name.Value
                : typeLookup.GetTypeName(
                    methodDescriptor.ResultSelection,
                    methodDescriptor.ResultType,
                    true);

            await writer.WriteIndentAsync();
            await writer.WriteAsync("private ");
            await writer.WriteAsync(resultTypeName);
            await writer.WriteSpaceAsync();
            await writer.WriteAsync("Parse");
            await writer.WriteAsync(methodDescriptor.Name);

            await writer.WriteAsync('(');
            await writer.WriteLineAsync();

            using (writer.IncreaseIndent())
            {
                await writer.WriteIndentAsync();
                await writer.WriteAsync("JsonElement parent,");
                await writer.WriteLineAsync();

                await writer.WriteIndentAsync();
                await writer.WriteAsync("ReadOnlySpan<byte> field)");
                await writer.WriteLineAsync();
            }

            await writer.WriteAsync('{');
            await writer.WriteLineAsync();

            using (writer.IncreaseIndent())
            {
                await writer.WriteIndentAsync();
                await writer.WriteAsync("if (!parent.TryGetProperty(field, out JsonElement obj))");
                await writer.WriteLineAsync();

                await writer.WriteIndentAsync();
                using (writer.WriteBraces())
                {
                    await writer.WriteIndentAsync();
                    await writer.WriteAsync("return null;");
                }
                await writer.WriteLineAsync();
                await writer.WriteLineAsync();

                await writer.WriteIndentAsync();
                await writer.WriteAsync("string type = obj.GetProperty(_typename).GetString();");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync();

                int last = methodDescriptor.PossibleTypes.Count - 1;

                for (int i = 0; i <= last; i++)
                {
                    var possibleType = methodDescriptor.PossibleTypes[i];

                    await writer.WriteIndentAsync();
                    await writer.WriteAsync("if (string.Equals(TypeName, ");
                    await writer.WriteStringValueAsync(possibleType.ResultDescriptor.Name);
                    await writer.WriteAsync(", StringComparison.Ordinal)");
                    await writer.WriteLineAsync();

                    await writer.WriteIndentAsync();
                    using (writer.WriteBraces())
                    {
                        if (methodDescriptor.ResultType.IsListType())
                        {
                            await WriteListAsync(
                                writer,
                                methodDescriptor,
                                possibleType,
                                "obj",
                                "element",
                                "list",
                                "entity",
                                typeLookup);
                        }
                        else
                        {
                            await WriteCreateObjectAsync(
                                writer,
                                methodDescriptor,
                                possibleType,
                                "obj",
                                typeLookup);
                        }
                    }

                    await writer.WriteLineAsync();

                    if (i < last)
                    {
                        await writer.WriteLineAsync();
                    }
                }

                await writer.WriteLineAsync();

                await writer.WriteIndentAsync();
                await writer.WriteAsync("throw new NotSupported(");
                await writer.WriteStringValueAsync("Handle not exhausted objects");
                await writer.WriteAsync(");");
                await writer.WriteLineAsync();

                writer.DecreaseIndent();
                await writer.WriteIndentAsync();
                await writer.WriteAsync('}');
                await writer.WriteLineAsync();
            }
        }

        private async Task WriteListAsync(
            CodeWriter writer,
            IResultParserMethodDescriptor methodDescriptor,
            IResultParserTypeDescriptor possibleType,
            string jsonElement,
            string elementField,
            string listField,
            string entityField,
            ITypeLookup typeLookup)
        {
            IType elementType = methodDescriptor.ResultType.ElementType();

            string resultTypeName = typeLookup.GetTypeName(
                methodDescriptor.ResultSelection,
                elementType,
                true);

            string lengthField = jsonElement + "Length";
            string indexField = jsonElement + "Index";

            await writer.WriteIndentAsync();
            await writer.WriteAsync("int ");
            await writer.WriteAsync(lengthField);
            await writer.WriteAsync(" = ");
            await writer.WriteAsync(jsonElement);
            await writer.WriteAsync(".GetArrayLength();");
            await writer.WriteLineAsync();

            await writer.WriteIndentAsync();
            await writer.WriteAsync("var ");
            await writer.WriteAsync(listField);
            await writer.WriteAsync(" = new ");
            await writer.WriteAsync(resultTypeName);
            await writer.WriteAsync('[');
            await writer.WriteAsync(lengthField);
            await writer.WriteAsync(']');
            await writer.WriteAsync(';');
            await writer.WriteLineAsync();
            await writer.WriteLineAsync();

            await writer.WriteIndentAsync();
            await writer.WriteAsync("for (int ");
            await writer.WriteAsync(indexField);
            await writer.WriteAsync(" = 0; ");
            await writer.WriteAsync(indexField);
            await writer.WriteAsync(" < arrayLength; ");
            await writer.WriteAsync(indexField);
            await writer.WriteAsync("++)");
            await writer.WriteLineAsync();

            await writer.WriteIndentAsync();
            using (writer.WriteBraces())
            {
                await writer.WriteIndentAsync();
                await writer.WriteAsync("JsonElement ");
                await writer.WriteAsync(elementField);
                await writer.WriteAsync(" = ");
                await writer.WriteAsync(jsonElement);
                await writer.WriteAsync('[');
                await writer.WriteAsync(indexField);
                await writer.WriteAsync(']');
                await writer.WriteAsync(';');
                await writer.WriteLineAsync();

                if (elementType.IsListType())
                {
                    await WriteListAsync(
                        writer,
                        methodDescriptor,
                        possibleType,
                        elementField,
                        "inner" + char.ToUpper(elementField[0]) + elementField.Substring(1),
                        "inner" + char.ToUpper(listField[0]) + listField.Substring(1),
                        "inner" + char.ToUpper(entityField[0]) + entityField.Substring(1),
                        typeLookup);
                }
                else
                {
                    await writer.WriteIndentAsync();
                    await writer.WriteAsync("var ");
                    await writer.WriteAsync(entityField);
                    await writer.WriteAsync(" = new ");
                    await writer.WriteAsync(possibleType.ResultDescriptor.Name);
                    await writer.WriteAsync("();");
                    await writer.WriteLineAsync();

                    await WriteObjectPropertyDeserializationAsync(
                        writer,
                        possibleType,
                        elementField,
                        entityField,
                        typeLookup);

                    await writer.WriteIndentAsync();
                    await writer.WriteAsync(listField);
                    await writer.WriteAsync('[');
                    await writer.WriteAsync(indexField);
                    await writer.WriteAsync(']');
                    await writer.WriteAsync(" = ");
                    await writer.WriteAsync(entityField);
                    await writer.WriteAsync(';');
                }
            }

            await writer.WriteLineAsync();
            await writer.WriteLineAsync();

            await writer.WriteIndentAsync();
            await writer.WriteAsync("return ");
            await writer.WriteAsync(listField);
            await writer.WriteAsync(';');
        }

        private async Task WriteCreateObjectAsync(
            CodeWriter writer,
            IResultParserMethodDescriptor methodDescriptor,
            IResultParserTypeDescriptor possibleType,
            string jsonElement,
            ITypeLookup typeLookup)
        {
            string entityField = GetFieldName(possibleType.ResultDescriptor.Name);

            await writer.WriteIndentAsync();
            await writer.WriteAsync("var ");
            await writer.WriteAsync(entityField);
            await writer.WriteAsync(" = new ");
            await writer.WriteAsync(possibleType.ResultDescriptor.Name);
            await writer.WriteAsync("();");
            await writer.WriteLineAsync();

            await WriteObjectPropertyDeserializationAsync(
                writer,
                possibleType,
                jsonElement,
                entityField,
                typeLookup);

            await writer.WriteIndentAsync();
            await writer.WriteAsync("return ");
            await writer.WriteAsync(entityField);
            await writer.WriteAsync('.');
        }

        private async Task WriteObjectPropertyDeserializationAsync(
           CodeWriter writer,
           IResultParserTypeDescriptor possibleType,
           string jsonElement,
           string entityField,
           ITypeLookup typeLookup)
        {
            foreach (IFieldDescriptor fieldDescriptor in possibleType.ResultDescriptor.Fields)
            {
                await writer.WriteIndentAsync();

                await writer.WriteAsync(entityField);
                await writer.WriteAsync('.');
                await writer.WriteAsync(GetPropertyName(fieldDescriptor.ResponseName));
                await writer.WriteAsync(" = ");

                if (fieldDescriptor.Type.NamedType().IsLeafType())
                {
                    string typeName = typeLookup.GetTypeName(
                        fieldDescriptor.Selection,
                        fieldDescriptor.Type,
                        true);

                    await writer.WriteAsync('(');
                    await writer.WriteAsync(typeName);
                    await writer.WriteAsync(')');
                    await writer.WriteAsync("Deserialize");
                    await writer.WriteAsync(fieldDescriptor.Type.NamedType().Name);
                    await writer.WriteAsync("Value");
                    await writer.WriteAsync('(');
                    await writer.WriteAsync(jsonElement);
                    await writer.WriteAsync(", \"");
                    await writer.WriteAsync(fieldDescriptor.ResponseName);
                    await writer.WriteAsync("\");");
                }
                else
                {
                    await writer.WriteAsync("Parse");
                    await writer.WriteAsync(GetPathName(fieldDescriptor.Path));
                    await writer.WriteAsync('(');
                    await writer.WriteAsync(jsonElement);
                    await writer.WriteAsync(", \"");
                    await writer.WriteAsync(fieldDescriptor.ResponseName);
                    await writer.WriteAsync("\");");
                }

                await writer.WriteLineAsync();
            }
        }
    }
}
