using System;
using System.Collections.Generic;
using System.Text;
using HotChocolate.Types.Descriptors.Definitions;

namespace HotChocolate.Types.Sorting
{
    public abstract class SortingNamingConventionBase : ISortingNamingConvention
    {
        public virtual NameString SortKindAscName { get; } = "ASC";
        public virtual NameString SortKindDescName { get; } = "DESC";

        public abstract NameString ArgumentName { get; }

        public static ISortingNamingConvention Default { get; } =
            new SortingNamingConventionSnakeCase();
    }
}
