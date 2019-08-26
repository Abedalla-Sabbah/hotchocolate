using System.Collections.Generic;
using HotChocolate.Language;

namespace StrawberryShake.Generators.Descriptors
{
    public interface IOperationDescriptor
        : ICodeDescriptor
    {
        OperationDefinitionNode Operation { get; }

        IReadOnlyList<IArgumentDescriptor> Arguments { get; }
    }
}
