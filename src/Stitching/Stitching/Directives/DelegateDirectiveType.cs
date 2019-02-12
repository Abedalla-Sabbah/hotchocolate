﻿using HotChocolate.Execution;
using HotChocolate.Types;

namespace HotChocolate.Stitching
{
    public class DelegateDirectiveType
        : DirectiveType<DelegateDirective>
    {
        protected override void Configure(
            IDirectiveTypeDescriptor<DelegateDirective> descriptor)
        {
            descriptor.Name(DirectiveNames.Delegate);

            descriptor.Location(Types.DirectiveLocation.FieldDefinition);

            descriptor.Argument(t => t.Path)
                .Name(DirectiveFieldNames.Delegate_Path)
                .Type<StringType>();

            descriptor.Argument(t => t.Schema)
                .Name(DirectiveFieldNames.Delegate_Schema)
                .Type<NonNullType<StringType>>()
                .Description("The name of the schema to which this " +
                    "field shall be delegated to.");
        }
    }
}
