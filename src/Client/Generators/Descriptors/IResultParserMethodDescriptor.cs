using System.Collections.Generic;
using HotChocolate;
using HotChocolate.Language;
using HotChocolate.Types;

namespace StrawberryShake.Generators
{
    public interface IResultParserMethodDescriptor
        : ICodeDescriptor
    {
        IType ResultType { get; }

        FieldNode ResultSelection { get; }

        Path Path { get; }

        IInterfaceDescriptor ResultDescriptor { get; }

        IResultParserTypeDescriptor UnknownType { get; }

        IReadOnlyList<IResultParserTypeDescriptor> PossibleTypes { get; }
    }
}
