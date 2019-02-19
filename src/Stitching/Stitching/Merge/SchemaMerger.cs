using System.Xml.Linq;
using System.Linq;
using System.Collections.Specialized;
using System;
using System.Collections.Generic;
using HotChocolate.Language;
using HotChocolate.Execution;
using HotChocolate.Stitching.Merge.Handlers;
using HotChocolate.Resolvers;
using HotChocolate.Stitching.Merge.Rewriters;

namespace HotChocolate.Stitching.Merge
{
    public class SchemaMerger
        : ISchemaMerger
    {
        private delegate T RewriteFieldsDelegate<T>(
            IReadOnlyList<FieldDefinitionNode> fields)
            where T : ComplexTypeDefinitionNodeBase, ITypeDefinitionNode;

        private static List<MergeTypeHandler> _defaultHandlers =
            new List<MergeTypeHandler>
            {
                SchemaMergerExtensions
                    .CreateHandler<ScalarTypeMergeHandler>(),
                SchemaMergerExtensions
                    .CreateHandler<InputObjectTypeMergeHandler>(),
                SchemaMergerExtensions
                    .CreateHandler<RootTypeMergeHandler>(),
                SchemaMergerExtensions
                    .CreateHandler<ObjectTypeMergeHandler>(),
                SchemaMergerExtensions
                    .CreateHandler<InterfaceTypeMergeHandler>(),
                SchemaMergerExtensions
                    .CreateHandler<UnionTypeMergeHandler>(),
                SchemaMergerExtensions
                    .CreateHandler<EnumTypeMergeHandler>(),
            };
        private readonly List<MergeTypeHandler> _handlers =
            new List<MergeTypeHandler>();
        private readonly List<ITypeRewriter> _typeRewriters =
            new List<ITypeRewriter>();
        private readonly List<IDocumentRewriter> _docRewriters =
            new List<IDocumentRewriter>();
        private readonly OrderedDictionary<NameString, DocumentNode> _schemas =
            new OrderedDictionary<NameString, DocumentNode>();

        public ISchemaMerger AddMergeHandler(MergeTypeHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _handlers.Add(handler);
            return this;
        }

        public ISchemaMerger AddSchema(NameString name, DocumentNode schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            name.EnsureNotEmpty(nameof(name));

            _schemas.Add(name, schema);

            return this;
        }

        public ISchemaMerger AddTypeRewriter(ITypeRewriter rewriter)
        {
            if (rewriter == null)
            {
                throw new ArgumentNullException(nameof(rewriter));
            }

            _typeRewriters.Add(rewriter);
            return this;
        }

        public ISchemaMerger AddDocumentRewriter(IDocumentRewriter rewriter)
        {
            if (rewriter == null)
            {
                throw new ArgumentNullException(nameof(rewriter));
            }

            _docRewriters.Add(rewriter);
            return this;
        }

        public DocumentNode Merge()
        {
            MergeTypeDelegate merge = CompileMergeDelegate();
            IReadOnlyList<ISchemaInfo> schemas = CreateSchemaInfos();

            var context = new SchemaMergeContext();

            // merge root types
            MergeRootType(context, OperationType.Query, schemas, merge);
            MergeRootType(context, OperationType.Mutation, schemas, merge);
            MergeRootType(context, OperationType.Subscription, schemas, merge);

            // merge all other types
            MergeTypes(context, CreateNameSet(schemas), schemas, merge);

            // TODO : FIX NAMES

            return context.CreateSchema();
        }

        private IReadOnlyList<ISchemaInfo> CreateSchemaInfos()
        {
            List<SchemaInfo> original = _schemas
                .Select(t => new SchemaInfo(t.Key, t.Value))
                .ToList();

            if (_docRewriters.Count == 0)
            {
                return original;
            }

            var rewritten = new List<SchemaInfo>();

            foreach (SchemaInfo schemaInfo in original)
            {
                DocumentNode current = schemaInfo.Document;

                foreach (IDocumentRewriter rewriter in _docRewriters)
                {
                    current = rewriter.Rewrite(schemaInfo, current);
                }

                if (current == schemaInfo.Document)
                {
                    rewritten.Add(schemaInfo);
                }
                else
                {
                    rewritten.Add(new SchemaInfo(
                        schemaInfo.Name,
                        current));
                }
            }

            return rewritten;
        }

        private void MergeRootType(
            ISchemaMergeContext context,
            OperationType operation,
            IEnumerable<ISchemaInfo> schemas,
            MergeTypeDelegate merge)
        {
            var types = new List<TypeInfo>();

            foreach (SchemaInfo schema in schemas)
            {
                ObjectTypeDefinitionNode rootType =
                    schema.GetRootType(operation);
                if (rootType != null)
                {
                    types.Add(new ObjectTypeInfo(rootType, schema));
                }
            }

            if (types.Count > 0)
            {
                merge(context, types);
            }
        }

        private void MergeTypes(
            ISchemaMergeContext context,
            ISet<string> typeNames,
            IEnumerable<ISchemaInfo> schemas,
            MergeTypeDelegate merge)
        {
            var types = new List<ITypeInfo>();

            foreach (string typeName in typeNames)
            {
                SetTypes(typeName, schemas, types);
                merge(context, types);
            }
        }

        private ISet<string> CreateNameSet(
            IEnumerable<ISchemaInfo> schemas)
        {
            HashSet<string> names = new HashSet<string>();

            foreach (SchemaInfo schema in schemas)
            {
                foreach (string name in schema.Types.Keys)
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private void SetTypes(
            string name,
            IEnumerable<ISchemaInfo> schemas,
            ICollection<ITypeInfo> types)
        {
            types.Clear();

            foreach (SchemaInfo schema in schemas)
            {
                if (schema.Types.TryGetValue(name,
                    out ITypeDefinitionNode typeDefinition))
                {
                    foreach (ITypeRewriter rewriter in _typeRewriters)
                    {
                        typeDefinition = rewriter.Rewrite(
                            schema, typeDefinition);
                    }

                    types.Add(TypeInfo.Create(typeDefinition, schema));
                }
            }
        }

        private MergeTypeDelegate CompileMergeDelegate()
        {
            MergeTypeDelegate current = (c, t) =>
            {
                if (t.Count > 0)
                {
                    throw new NotSupportedException(
                        "The type definitions could not be handled.");
                }
            };

            var handlers = new List<MergeTypeHandler>();
            handlers.AddRange(_defaultHandlers);
            handlers.AddRange(_handlers);

            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                current = handlers[i].Invoke(current);
            }

            return current;
        }

        public static SchemaMerger New() => new SchemaMerger();
    }
}
