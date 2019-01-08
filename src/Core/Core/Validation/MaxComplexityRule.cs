using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HotChocolate.Execution.Configuration;
using HotChocolate.Language;
using HotChocolate.Types;

namespace HotChocolate.Validation
{
    internal sealed class MaxComplexityRule
        : IQueryValidationRule
    {
        private readonly IValidateQueryOptionsAccessor _options;
        private readonly ComplexityComputation _calculateComplexity;
        private readonly MaxComplexityVisitor _visitor =
            new MaxComplexityVisitor();

        public MaxComplexityRule(
            IValidateQueryOptionsAccessor options,
            ComplexityComputation calculateComplexity)
        {
            _options = options
                ?? throw new ArgumentNullException(nameof(options));
            _calculateComplexity = calculateComplexity
                ?? new ComplexityComputation(
                    (fieldDef, field, path, cost) => cost.Complexity);
        }

        public QueryValidationResult Validate(
            ISchema schema,
            DocumentNode queryDocument)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            if (queryDocument == null)
            {
                throw new ArgumentNullException(nameof(queryDocument));
            }

            int complexity = _visitor.Visit(
                queryDocument,
                schema,
                _calculateComplexity);

            if (complexity > _options.MaxOperationComplexity)
            {
                return new QueryValidationResult(new ValidationError(
                    "At least one operation of the query document had a " +
                    $"complexity of {complexity}. \n" +
                    "The maximum allowed query complexity is " +
                    $"{_options.MaxOperationComplexity}."));
            }

            return QueryValidationResult.OK;
        }
    }

    internal sealed class MaxComplexityVisitor
        : QuerySyntaxWalker<MaxComplexityVisitor.Context>
    {
        protected override bool VisitFragmentDefinitions => false;

        public int Visit(
            DocumentNode node,
            ISchema schema,
            ComplexityComputation calculateComplexity)
        {
            var context = Context.New(schema, calculateComplexity);
            Visit(node, context);
            return context.MaxComplexity;
        }

        protected override void VisitDocument(
            DocumentNode node,
            MaxComplexityVisitor.Context context)
        {
            foreach (var fragment in node.Definitions
                .OfType<FragmentDefinitionNode>()
                .Where(t => t.Name?.Value != null))
            {
                context.Fragments[fragment.Name.Value] = fragment;
            }

            foreach (var operation in node.Definitions
                .OfType<OperationDefinitionNode>())
            {
                if (TryGetOperationType(
                    context.Schema,
                    operation.Operation,
                    out ObjectType objectType))
                {
                    VisitOperationDefinition(
                        operation,
                        context.SetTypeContext(objectType));
                }
            }
        }

        protected override void VisitField(
            FieldNode field,
            MaxComplexityVisitor.Context context)
        {
            Context newContext = context;

            if (context.TypeContext is IComplexOutputType type
                && type.Fields.TryGetField(field.Name.Value,
                    out IOutputField fieldDefinition))
            {
                newContext = newContext.AddField(fieldDefinition, field);

                if (fieldDefinition.Type.NamedType() is IComplexOutputType ct)
                {
                    newContext = newContext.SetTypeContext(ct);
                }

                base.VisitField(field, newContext);
            }
        }

        protected override void VisitFragmentSpread(
            FragmentSpreadNode node,
            MaxComplexityVisitor.Context context)
        {
            base.VisitFragmentSpread(node, context);

            if (context.Fragments.TryGetValue(node.Name.Value,
                out FragmentDefinitionNode fragment))
            {
                VisitFragmentDefinition(fragment, context);
            }
        }

        protected override void VisitFragmentDefinition(
            FragmentDefinitionNode node, Context context)
        {
            Context newContext = context;

            if (newContext.Schema.TryGetType(
                node.TypeCondition.Name.Value,
                out IComplexOutputType type))
            {

                newContext = newContext.SetTypeContext(type);
            }

            base.VisitFragmentDefinition(node, newContext);
        }

        protected override void VisitInlineFragment(
            InlineFragmentNode node, Context context)
        {
            Context newContext = context;

            if (newContext.Schema.TryGetType(
                node.TypeCondition.Name.Value,
                out IComplexOutputType type))
            {
                newContext = newContext.SetTypeContext(type);
            }

            base.VisitInlineFragment(node, newContext);
        }

        protected override void VisitFieldDefinition(
            FieldDefinitionNode node,
            MaxComplexityVisitor.Context context)
        {
            if (!context.FragmentPath.Contains(node.Name.Value))
            {
                base.VisitFieldDefinition(node, context);
            }
        }

        private static bool TryGetOperationType(
            ISchema schema,
            OperationType operation,
            out ObjectType objectType)
        {
            switch (operation)
            {
                case OperationType.Query:
                    objectType = schema.QueryType;
                    break;

                case OperationType.Mutation:
                    objectType = schema.MutationType;
                    break;

                case Language.OperationType.Subscription:
                    objectType = schema.SubscriptionType;
                    break;

                default:
                    objectType = null;
                    break;
            }

            return objectType != null;
        }

        internal sealed class Context
        {
            private readonly Context _root;
            private readonly ComplexityComputation _calculateComplexity;
            private readonly int _complexity;
            private int _maxComplexity;

            private Context(
                ISchema schema,
                ComplexityComputation calculateComplexity)
            {
                _calculateComplexity = calculateComplexity;
                Schema = schema;
                FragmentPath = ImmutableHashSet<string>.Empty;
                FieldPath = ImmutableList<IOutputField>.Empty;
                Fragments = new Dictionary<string, FragmentDefinitionNode>();
                _root = this;
            }

            private Context(
                ImmutableHashSet<string> fragmentPath,
                ImmutableList<IOutputField> fieldPath,
                int complexity,
                Context context)
            {
                FragmentPath = fragmentPath;
                FieldPath = fieldPath;
                Schema = context.Schema;
                Fragments = context.Fragments;
                TypeContext = context.TypeContext;
                _complexity = complexity;
                _calculateComplexity = context._calculateComplexity;
                _root = context._root;
            }

            private Context(Context context)
            {
                Schema = context.Schema;
                FragmentPath = context.FragmentPath;
                FieldPath = context.FieldPath;
                Fragments = context.Fragments;
                TypeContext = context.TypeContext;
                _complexity = context._complexity;
                _calculateComplexity = context._calculateComplexity;
                _root = context._root;
            }

            public ISchema Schema { get; }

            public ImmutableHashSet<string> FragmentPath { get; }

            public ImmutableList<IOutputField> FieldPath { get; }

            public INamedOutputType TypeContext { get; private set; }

            public IDictionary<string, FragmentDefinitionNode> Fragments
            { get; }

            public int Complexity => _complexity;

            public int MaxComplexity => _root._maxComplexity;

            public Context AddFragment(FragmentDefinitionNode fragment)
            {
                return new Context(
                    FragmentPath.Add(fragment.Name.Value),
                    FieldPath,
                    _complexity,
                    this);
            }

            public Context AddField(
                IOutputField fieldDefinition,
                FieldNode fieldSelection)
            {
                IDirective directive = fieldDefinition.Directives
                    .FirstOrDefault(t => t.Type is CostDirectiveType);
                int complexity;

                if (directive == null)
                {
                    complexity = _complexity + 1;
                }
                else
                {
                    var cost = directive.ToObject<CostDirective>();
                    complexity = _complexity + _calculateComplexity(
                        fieldDefinition, fieldSelection, FieldPath, cost);
                }

                if (complexity > _root._maxComplexity)
                {
                    _root._maxComplexity = complexity;
                }

                return new Context(
                    FragmentPath,
                    FieldPath.Add(fieldDefinition),
                    complexity,
                    this);
            }

            public Context SetTypeContext(INamedOutputType typeContext)
            {
                var newContext = new Context(this);
                newContext.TypeContext = typeContext;
                return newContext;
            }

            public static Context New(
                ISchema schema,
                ComplexityComputation calculateComplexity) =>
                    new Context(schema, calculateComplexity);
        }
    }

    public delegate int ComplexityComputation(
        IOutputField fieldDefinition,
        FieldNode fieldSelection,
        ICollection<IOutputField> path,
        CostDirective cost);
}
