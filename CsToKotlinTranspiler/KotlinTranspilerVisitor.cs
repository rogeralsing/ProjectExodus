// -----------------------------------------------------------------------
//   <copyright file="KotlinTranspilerVisitor.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsToKotlinTranspiler
{
    public partial class KotlinTranspilerVisitor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _model;
        private Dictionary<ISymbol, AssignmentExpressionSyntax[]> _assignments;

        public KotlinTranspilerVisitor(SemanticModel model, SyntaxWalkerDepth depth = SyntaxWalkerDepth.Node) :
            base(depth)
        {
            _model = model;
        }

        public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            base.VisitConversionOperatorDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var arg = GetArgList(node.ParameterList);
            IndentWrite($"constructor({arg}) ");
            if (node.Body != null)
            {
                Visit(node.Body);
            }
            else
            {
                Visit(node.ExpressionBody);
                NewLine(); //should maybe be in the arrow expression visit?
            }
        }

        public override void VisitConstructorInitializer(ConstructorInitializerSyntax node)
        {
            base.VisitConstructorInitializer(node);
        }

        public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            base.VisitDestructorDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var name = ToCamelCase(node.Identifier.Text);
            var t = TranslateType(node.Type);

            WriteModifiers(node.Modifiers);
            if (IsInterfaceProperty(node))
            {
                Write("override ");
            }

            if (node.AccessorList != null)
            {
                var accessors = node.AccessorList.Accessors.Select(a => a.Keyword.Text).ToImmutableHashSet();
                Write(accessors.Contains("set") ? "var " : "val ");
            }
            else
            {
                Write("val ");
            }

            Write($"{name} : {t}");
            if (node.Initializer != null)
            {
                Write(" = ");
                Visit(node.Initializer.Value);
            }

            if (node.ExpressionBody != null)
            {
                _indent++;
                NewLine();
                IndentWrite("get() = ");
                Visit(node.ExpressionBody.Expression);
                _indent--;
            }

            NewLine();
        }

        public override void VisitTypeConstraint(TypeConstraintSyntax node)
        {
            base.VisitTypeConstraint(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            foreach (var v in node.Declaration.Variables)
            {
                var t = TranslateType(node.Declaration.Type);
                if (t == "Unit")
                {
                    continue;
                }

                WriteModifiers(node.Modifiers);
                var isMutated = IsMutated(v);
                var isReadOnly = FieldIsReadOnly(node);


                var d = TranslateDefaultValue(node.Declaration.Type);
                var nullable = v.Initializer == null && !isReadOnly;
                if (v.Initializer != null)
                {
                    Write(isReadOnly || !isMutated ? "val" : "var"); //readonly, const, non mutated fields
                    Write($" {v.Identifier} : {t} = ");
                    Visit(v.Initializer.Value);
                }
                else if (d != null)
                {
                    Write(isReadOnly || !isMutated ? "val" : "var"); //readonly, const, non mutated fields
                    Write($" {v.Identifier} : {t} = {d}");
                }
                else if (nullable)
                {
                    Write($"lateinit var {v.Identifier} : {t}");
                }
                else
                {
                    Write($" {v.Identifier} : {t}");
                }

                NewLine();
            }
        }

        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            base.VisitEventFieldDeclaration(node);
        }

        public override void VisitExplicitInterfaceSpecifier(ExplicitInterfaceSpecifierSyntax node)
        {
            base.VisitExplicitInterfaceSpecifier(node);
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            //   base.VisitUsingDirective(node);
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            IndentWriteLine($"package {GetKotlinPackageName(node.Name.ToString())}");
            //base.VisitNamespaceDeclaration(node);
            foreach (var m in node.Members)
            {
                Visit(m);
                NewLine();
            }
        }

        public override void VisitAttributeList(AttributeListSyntax node)
        {
            base.VisitAttributeList(node);
        }

        public override void VisitAttributeTargetSpecifier(AttributeTargetSpecifierSyntax node)
        {
            base.VisitAttributeTargetSpecifier(node);
        }

        public override void VisitTypeParameter(TypeParameterSyntax node)
        {
            base.VisitTypeParameter(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (node.Modifiers.Contains("static"))
            {
                WriteObject(node);
            }
            else
            {
                WriteClass(node);
            }
        }

        private void WriteObject(ClassDeclarationSyntax node)
        {
            NewLine();
            WriteClassModifiers(node.Modifiers);
            Write($"object {node.Identifier}");
            Write(" {");
            NewLine();
            _indent++;

            foreach (var m in node.Members)
            {
                Visit(m);
            }

            _indent--;
            IndentWriteLine("}");
        }

        private void WriteClass(ClassDeclarationSyntax node)
        {
            NewLine();
            WriteClassModifiers(node.Modifiers);
            Write($"class {node.Identifier}");

            if (node.BaseList != null)
            {
                Write(" : ");
                Delimit(node.BaseList.Types, t =>
                {
                    var tn = TranslateType(t.Type);
                    Write(tn);
                });
            }

            Write(" {");
            NewLine();
            _indent++;
            var statics = node.Members.Where(mm =>
            {
                if (mm is FieldDeclarationSyntax field &&
                    (field.Modifiers.Contains("const") || field.Modifiers.Contains("static")))
                {
                    return true;
                }

                var mmm = _model.GetDeclaredSymbol(mm);
                return mmm?.IsStatic == true;
            }).ToList();
            var instance = node.Members.Except(statics).ToList();

            if (statics.Any())
            {
                IndentWriteLine("companion object {");
                _indent++;
                foreach (var m in statics)
                {
                    Visit(m);
                }

                _indent--;
                IndentWriteLine("}");
            }

            foreach (var m in instance)
            {
                Visit(m);
            }

            _indent--;
            IndentWriteLine("}");
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            base.VisitStructDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            NewLine();
            WriteModifiers(node.Modifiers);
            var name = TranslateInterfaceType(node.Identifier.Text);
            Write($"interface {name} {{");
            NewLine();
            _indent++;
            foreach (var m in node.Members)
            {
                Visit(m);
            }

            _indent--;
            IndentWriteLine("}");
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            NewLine();
            WriteModifiers(node.Modifiers);
            Write($"enum class {node.Identifier.Text} {{");
            NewLine();

            _indent++;
            Indent();
            Delimit(node.Members, m => { Write(m.Identifier.Text); });
            _indent--;
            NewLine();

            IndentWriteLine("}");
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            //pass
        }


        public override void VisitBaseList(BaseListSyntax node)
        {
            base.VisitBaseList(node);
        }

        public override void VisitSimpleBaseType(SimpleBaseTypeSyntax node)
        {
            base.VisitSimpleBaseType(node);
        }

        public override void VisitTypeParameterConstraintClause(TypeParameterConstraintClauseSyntax node)
        {
            base.VisitTypeParameterConstraintClause(node);
        }

        public override void VisitConstructorConstraint(ConstructorConstraintSyntax node)
        {
            base.VisitConstructorConstraint(node);
        }

        public override void VisitClassOrStructConstraint(ClassOrStructConstraintSyntax node)
        {
            base.VisitClassOrStructConstraint(node);
        }

        public override void VisitEqualsValueClause(EqualsValueClauseSyntax node)
        {
            Visit(node.Value);
        }

        public override void VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
        {
            Write(node.Identifier.Text);
        }

        public override void VisitParenthesizedVariableDesignation(ParenthesizedVariableDesignationSyntax node)
        {
            Write("(");
            Delimit(node.Variables, v =>
            {
                if (v is SingleVariableDesignationSyntax single)
                {
                    Write(single.Identifier.Text);
                }

                if (v is DiscardDesignationSyntax _)
                {
                    Write("_");
                }
            });
            Write(")");
        }

        public override void VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            Indent();
            base.VisitExpressionStatement(node);
            NewLine();
        }

        public override void VisitEmptyStatement(EmptyStatementSyntax node)
        {
        }

        public override void VisitLabeledStatement(LabeledStatementSyntax node)
        {
            base.VisitLabeledStatement(node);
        }

        public override void VisitGotoStatement(GotoStatementSyntax node)
        {
            base.VisitGotoStatement(node);
        }

        public override void VisitBreakStatement(BreakStatementSyntax node)
        {
            SyntaxNode c = node;
            if (!IsSwitchBreak(c))
            {
                IndentWriteLine("break");
            }
        }

        private static bool IsSwitchBreak(SyntaxNode c)
        {
            while (c.Parent != null)
            {
                switch (c)
                {
                    case SwitchStatementSyntax _:
                        return true;
                    case DoStatementSyntax _:
                    case ForEachStatementSyntax _:
                    case ForStatementSyntax _:
                        return false;
                }

                c = c.Parent;
            }

            return false;
        }

        public override void VisitContinueStatement(ContinueStatementSyntax node)
        {
            IndentWriteLine("continue");
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            IndentWrite("return ");
            Visit(node.Expression);
            NewLine();
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            IndentWrite("throw ");
            Visit(node.Expression);
            NewLine();
        }

        public override void VisitYieldStatement(YieldStatementSyntax node)
        {
            if (node.Kind() == SyntaxKind.YieldBreakStatement)
            {
                IndentWrite("return");
                NewLine();
            }

            if (node.Kind() == SyntaxKind.YieldReturnStatement)
            {
                IndentWrite("yield (");
                Visit(node.Expression);
                Write(")");
                NewLine();
            }
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            IndentWrite("while (");
            Visit(node.Condition);
            Write(")");
            VisitMaybeBlock(node.Statement);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            var b = node.Statement as BlockSyntax;
            IndentWrite("do ");
            VisitInlineBlock(b);
            if (node.Condition != null)
            {
                Write(" while (");
                Visit(node.Condition);
                Write(")");
            }

            NewLine();
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            if (node?.Declaration.Variables.Count == 1 &&
                node?.Declaration.Variables.First() is VariableDeclaratorSyntax init &&
                node?.Incrementors.Count == 1 &&
                node?.Incrementors.First() is PostfixUnaryExpressionSyntax inc &&
                inc.Kind() == SyntaxKind.PostIncrementExpression &&
                node.Condition is BinaryExpressionSyntax guard)
            {
                IndentWrite($"for ({init.Identifier.Text} in ");

                switch (guard.Kind())
                {
                    case SyntaxKind.LessThanExpression:
                        Visit(init.Initializer.Value);
                        Write(" until ");
                        Visit(guard.Right);
                        break;
                    case SyntaxKind.LessThanOrEqualExpression:
                        Visit(init.Initializer.Value);
                        Write("..");
                        Visit(guard.Right);
                        break;
                }

                Write(")");
                VisitMaybeBlock(node.Statement);
            }
            else
            {
                IndentWriteLine("*** Unknown for statement ***");
            }
        }


        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            IndentWrite("for(");
            Write(node.Identifier.ToString());
            Write(" in ");
            Visit(node.Expression);
            Write(")");
            VisitMaybeBlock(node.Statement);
        }

        public override void VisitUsingStatement(UsingStatementSyntax node)
        {
            //Not supported
        }

        public override void VisitFixedStatement(FixedStatementSyntax node)
        {
            //Not supported
        }

        public override void VisitCheckedStatement(CheckedStatementSyntax node)
        {
            //Not supported
        }

        public override void VisitUnsafeStatement(UnsafeStatementSyntax node)
        {
            //Not supported
        }

        public override void VisitLockStatement(LockStatementSyntax node)
        {
            Indent();

            Write("synchronized(");
            Visit(node.Expression);
            Write(",{");
            VisitMaybeInlineBlock(node.Statement);
            Write(")");
            NewLine();
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            Indent();
            VisitInlineIfStatement(node);
        }

        public void VisitInlineIfStatement(IfStatementSyntax node)
        {
            Write("if (");
            Visit(node.Condition);
            Write(")");
            VisitMaybeInlineBlock(node.Statement);
            if (node.Else == null)
            {
                NewLine();
            }
            else if (node.Else.Statement is IfStatementSyntax elseif)
            {
                Write(" else ");
                VisitInlineIfStatement(elseif);
            }
            else
            {
                Write(" else");
                VisitMaybeBlock(node.Else.Statement);
            }
        }

        private void VisitMaybeInlineBlock(StatementSyntax node)
        {
            if (node is BlockSyntax block)
            {
                VisitInlineBlock(block);
            }
            else
            {
                _indent++;
                NewLine();
                Visit(node);
                _indent--;
            }
        }

        private void VisitMaybeBlock(StatementSyntax node)
        {
            if (node is BlockSyntax)
            {
                Visit(node);
            }
            else
            {
                _indent++;
                NewLine();
                Visit(node);
                _indent--;
            }
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            IndentWrite("val tmp = ");
            Visit(node.Expression);
            NewLine();
            IndentWriteLine("when (tmp) {");
            _indent++;
            foreach (var s in node.Sections)
            {
                Visit(s);
            }

            _indent--;
            IndentWriteLine("}");
        }

        private void Delimit<T>(IEnumerable<T> list, Action<T> action, string delimiter = ", ")
        {
            var first = true;
            foreach (var item in list)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Write(delimiter);
                }

                action(item);
            }
        }

        public override void VisitSwitchSection(SwitchSectionSyntax node)
        {
            var label = node.Labels.First();
            switch (label)
            {
                case CasePatternSwitchLabelSyntax _:
                {
                    IndentWrite("is ");

                    Delimit(node.Labels, i =>
                    {
                        var c = i as CasePatternSwitchLabelSyntax;
                        var d = c.Pattern as DeclarationPatternSyntax;

                        var t = TranslateType(d.Type);
                        Write(t);
                    });

                    Write(" -> {");
                    NewLine();
                    _indent++;
                    foreach (CasePatternSwitchLabelSyntax c in node.Labels)
                    {
                        var d = c.Pattern as DeclarationPatternSyntax;
                        if (d.Designation is SingleVariableDesignationSyntax v)
                        {
                            IndentWriteLine($"val {v.Identifier.Text} = tmp");
                        }
                    }

                    foreach (var s in GetUnwrappedStatements(node.Statements))
                    {
                        Visit(s);
                    }

                    _indent--;
                    IndentWriteLine("}");
                    return;
                }
                case DefaultSwitchLabelSyntax l:
                {
                    IndentWriteLine("else -> {");
                    _indent++;
                    foreach (var s in GetUnwrappedStatements(node.Statements))
                    {
                        Visit(s);
                    }

                    _indent--;
                    IndentWriteLine("}");
                    return;
                }
                case CaseSwitchLabelSyntax _:
                {
                    Indent();
                    Delimit(node.Labels, l =>
                    {
                        var x = l as CaseSwitchLabelSyntax;
                        Visit(x.Value);
                    });
                    Write(" -> {");
                    NewLine();
                    _indent++;
                    foreach (var s in GetUnwrappedStatements(node.Statements))
                    {
                        Visit(s);
                    }

                    _indent--;
                    IndentWriteLine("}");
                    return;
                }
                default:
                {
                    Write("Unknown case");
                    return;
                }
            }
        }

        private static SyntaxList<StatementSyntax> GetUnwrappedStatements(SyntaxList<StatementSyntax> statements)
        {
            if (statements.Count == 1 && statements.First() is BlockSyntax block)
            {
                return block.Statements;
            }

            return statements;
        }

        public override void VisitCasePatternSwitchLabel(CasePatternSwitchLabelSyntax node)
        {
            base.VisitCasePatternSwitchLabel(node);
        }

        public override void VisitConstantPattern(ConstantPatternSyntax node)
        {
            base.VisitConstantPattern(node);
        }

        public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
        {
            base.VisitCaseSwitchLabel(node);
        }

        public override void VisitDefaultSwitchLabel(DefaultSwitchLabelSyntax node)
        {
            base.VisitDefaultSwitchLabel(node);
        }

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            IndentWrite("try ");
            Visit(node.Block);
            foreach (var c in node.Catches)
            {
                var v = c.Declaration.Identifier.Text;
                var t = TranslateType(c.Declaration.Type);
                IndentWrite($"catch ({v} : {t})");
                Visit(c.Block);
            }
        }


        public override void VisitExternAliasDirective(ExternAliasDirectiveSyntax node)
        {
            //not supported
            base.VisitExternAliasDirective(node);
        }

        public override void VisitSizeOfExpression(SizeOfExpressionSyntax node)
        {
            base.VisitSizeOfExpression(node);
        }

        public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            base.VisitElementAccessExpression(node);
        }

        public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
        {
            Visit(node.Operand);
            Write(node.OperatorToken.Text);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var memberName = node.Name.ToString();
            var sym = _model.GetSymbolInfo(node).Symbol;
            var containingTypeName = sym?.ContainingType?.Name;
            if (sym.Kind == SymbolKind.Method)
            {
            }

            Visit(node.Expression);
            Write(".");
            var name = memberName;
            if (name == "Length" || name == "Count")
            {
                Write("count()");
                return;
            }

            if (sym.Kind == SymbolKind.Method || sym.Kind == SymbolKind.Property)
            {
                name = ToCamelCase(name);
            }

            Write(name);
        }

        public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            Visit(node.Expression);
            Write("?.");
            Visit(node.WhenNotNull);
        }

        private bool IsAsync(TypeSyntax type)
        {
            var s = GetTypeSymbol(type);
            return IsAsync(s);
        }

        private static bool IsAsync(ITypeSymbol s) => s is INamedTypeSymbol named && named.Name == "Task";

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var arg = GetArgList(node.ParameterList);
            var methodName = ToCamelCase(node.Identifier.Text);
            var ret = TranslateType(node.ReturnType);
            WriteModifiers(node.Modifiers);

            if (IsAsync(node.ReturnType))
            {
                Write("suspend ");
            }

            if (IsInterfaceMethod(node))
            {
                Write("override ");
            }

            if (ret == "Unit")
            {
                Write($"fun {methodName} ({arg})");
            }
            else
            {
                Write($"fun {methodName} ({arg}) : {ret}");
            }

            if (node.Body != null)
            {
                Visit(node.Body);
            }
            else if (node.ExpressionBody != null)
            {
                Visit(node.ExpressionBody);
                NewLine();
            }
            else
            {
                NewLine(); //interface method
            }
        }


        private void WriteClassModifiers(SyntaxTokenList modifiers)
        {
            Indent();
            if (!modifiers.Contains("sealed") && !modifiers.Contains("abstract") && !modifiers.Contains("static"))
            {
                Write("open ");
            }

            if (modifiers.Contains("private"))
            {
                Write("private ");
            }

            if (modifiers.Contains("protected"))
            {
                Write("protected ");
            }

            if (modifiers.Contains("internal"))
            {
                Write("internal ");
            }

            if (modifiers.Contains("abstract"))
            {
                Write("abstract ");
            }
        }

        private void WriteModifiers(SyntaxTokenList modifiers)
        {
            Indent();

            if (modifiers.Contains("private"))
            {
                Write("private ");
            }

            if (modifiers.Contains("protected"))
            {
                Write("protected ");
            }

            if (modifiers.Contains("internal"))
            {
                Write("internal ");
            }

            if (modifiers.Contains("abstract"))
            {
                Write("abstract ");
            }
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            base.VisitOperatorDeclaration(node);
        }


        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            var si = _model.GetSymbolInfo(node);
            var sym = si.Symbol;

            if (sym == null)
            {
                Write(node.Identifier.Text);
            }
            else if (sym.Kind == SymbolKind.Method || sym.Kind == SymbolKind.Property)
            {
                var name = ToCamelCase(node.Identifier.Text);
                Write(name);
            }
            else
            {
                var name = node.Identifier.Text;
                Write(name);
            }
        }

        public override void VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
        {
            base.VisitMemberBindingExpression(node);
        }

        public override void VisitElementBindingExpression(ElementBindingExpressionSyntax node)
        {
            base.VisitElementBindingExpression(node);
        }

        public override void VisitImplicitElementAccess(ImplicitElementAccessSyntax node)
        {
            base.VisitImplicitElementAccess(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            Visit(node.Left);
            Write(" ");
            if (node.Kind() == SyntaxKind.CoalesceExpression)
            {
                Write("?:");
            }
            else
            {
                Write(node.OperatorToken.Text);
            }

            Write(" ");
            Visit(node.Right);
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            base.VisitAccessorDeclaration(node);
        }

        public override void VisitParameterList(ParameterListSyntax node)
        {
            base.VisitParameterList(node);
        }

        public override void VisitBracketedParameterList(BracketedParameterListSyntax node)
        {
            base.VisitBracketedParameterList(node);
        }

        public override void VisitParameter(ParameterSyntax node)
        {
            base.VisitParameter(node);
        }

        public override void VisitIncompleteMember(IncompleteMemberSyntax node)
        {
            base.VisitIncompleteMember(node);
        }

        public override void VisitSkippedTokensTrivia(SkippedTokensTriviaSyntax node)
        {
            base.VisitSkippedTokensTrivia(node);
        }

        public override void VisitDocumentationCommentTrivia(DocumentationCommentTriviaSyntax node)
        {
            base.VisitDocumentationCommentTrivia(node);
        }


        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            base.VisitIndexerDeclaration(node);
        }

        public override void VisitAccessorList(AccessorListSyntax node)
        {
            base.VisitAccessorList(node);
        }

        public override void VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
        {
            base.VisitAliasQualifiedName(node);
        }

        public override void VisitPredefinedType(PredefinedTypeSyntax node)
        {
            base.VisitPredefinedType(node);
        }

        public override void VisitCastExpression(CastExpressionSyntax node)
        {
            Visit(node.Expression);
            Write(" as ");
            var t = TranslateType(node.Type);
            Write(t);
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            base.VisitAnonymousMethodExpression(node);
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            //var t = _model.GetSymbolInfo(node).Symbol as IMethodSymbol;
            //if (IsAsync(t.ReturnType))
            //{
            //    Write("suspend ");
            //}
            if (node.Body is BinaryExpressionSyntax bin && bin.Left is IdentifierNameSyntax name)
            {
                Write("{");
                Write("it " + bin.OperatorToken + " ");
                Visit(bin.Right);
                Write("}");
            }
            else if (node.Body is BlockSyntax block)
            {
                Write("{");
                Write(node.Parameter.Identifier.ToString());
                Write(" -> ");
                NewLine();
                _indent++;
                foreach (var s in block.Statements)
                {
                    Visit(s);
                }

                _indent--;
                IndentWriteLine("}");
            }
            else
            {
                Write("{");
                Write(node.Parameter.Identifier.ToString());
                Write(" -> ");
                Visit(node.Body);
                Write("}");
            }
        }

        public override void VisitRefExpression(RefExpressionSyntax node)
        {
            base.VisitRefExpression(node);
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            //var t = _model.GetSymbolInfo(node).Symbol as IMethodSymbol;
            //if (IsAsync(t.ReturnType))
            //{
            //    Write("suspend ");
            //}
            Write("{");
            if (node.ParameterList.Parameters.Any())
            {
                var arg = GetArgList(node.ParameterList);
                Write(arg);
                Write(" -> ");
            }
            else
            {
                Write(" ");
            }

            if (node.Body is BlockSyntax block)
            {
                NewLine();
                _indent++;
                foreach (var s in block.Statements)
                {
                    Visit(s);
                }

                _indent--;
                IndentWriteLine(" }");
            }
            else
            {
                Visit(node.Body);
                Write(" }");
            }
        }

        public override void VisitInitializerExpression(InitializerExpressionSyntax node)
        {
            if (node.Parent is ObjectCreationExpressionSyntax parent)
            {
                var t = _model.GetSymbolInfo(parent.Type).Symbol;
                if (t?.Name == nameof(List<object>))
                {
                    Write("listOf(");
                    Delimit(node.Expressions, Visit);
                    Write(")");
                    return;
                }

                Write("().apply {");
                Delimit(node.Expressions, Visit, "; ");
                Write("}");
                return;
            }

            Write("arrayOf(");
            Delimit(node.Expressions, Visit);
            Write(")");
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var t = TranslateObjectCreator(node.Type);
            Write(t);
            Visit(node.ArgumentList);
        }

        public override void VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
        {
            base.VisitAnonymousObjectCreationExpression(node);
        }

        public override void VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
        {
            base.VisitAnonymousObjectMemberDeclarator(node);
        }

        public override void VisitBracketedArgumentList(BracketedArgumentListSyntax node)
        {
            Write("[");
            Delimit(node.Arguments, Visit);
            Write("]");
        }

        public override void VisitArgument(ArgumentSyntax node)
        {
            base.VisitArgument(node);
        }

        public override void VisitNameColon(NameColonSyntax node)
        {
            base.VisitNameColon(node);
        }

        public override void VisitArgumentList(ArgumentListSyntax node)
        {
            //this is a method call where there is a single arg which is a lambda.
            //thus we can remove the parens around it
            if (node.Arguments.Count == 1)
            {
                var arg = node.Arguments.First();
                var t = _model.GetSymbolInfo(arg.Expression);
                var sym = t.Symbol;
                if (sym != null &&
                    sym.ToString() == "lambda expression") //TODO: I have no idea how to check this correctly
                {
                    Visit(arg);
                    return;
                }
            }

            Write("(");
            Delimit(node.Arguments, Visit);
            Write(")");
        }

        public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
        {
            var t = TranslateType(node.Type);
            Write(t + "()");
            //TODO: handle initializer
        }

        public override void VisitImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node)
        {
            base.VisitImplicitArrayCreationExpression(node);
        }

        public override void VisitStackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node)
        {
            base.VisitStackAllocArrayCreationExpression(node);
        }

        public override void VisitQueryExpression(QueryExpressionSyntax node)
        {
            base.VisitQueryExpression(node);
        }

        public override void VisitQueryBody(QueryBodySyntax node)
        {
            base.VisitQueryBody(node);
        }

        public override void VisitFromClause(FromClauseSyntax node)
        {
            base.VisitFromClause(node);
        }

        public override void VisitLetClause(LetClauseSyntax node)
        {
            base.VisitLetClause(node);
        }

        public override void VisitJoinClause(JoinClauseSyntax node)
        {
            base.VisitJoinClause(node);
        }

        public override void VisitJoinIntoClause(JoinIntoClauseSyntax node)
        {
            base.VisitJoinIntoClause(node);
        }

        public override void VisitWhereClause(WhereClauseSyntax node)
        {
            base.VisitWhereClause(node);
        }

        public override void VisitOrderByClause(OrderByClauseSyntax node)
        {
            base.VisitOrderByClause(node);
        }

        public override void VisitOrdering(OrderingSyntax node)
        {
            base.VisitOrdering(node);
        }

        public override void VisitSelectClause(SelectClauseSyntax node)
        {
            base.VisitSelectClause(node);
        }

        public override void VisitGroupClause(GroupClauseSyntax node)
        {
            base.VisitGroupClause(node);
        }

        public override void VisitQueryContinuation(QueryContinuationSyntax node)
        {
            base.VisitQueryContinuation(node);
        }

        public override void VisitOmittedArraySizeExpression(OmittedArraySizeExpressionSyntax node)
        {
            base.VisitOmittedArraySizeExpression(node);
        }

        public override void VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            Write("\"");
            foreach (var i in node.Contents)
            {
                Visit(i);
            }

            Write("\"");
        }

        public override void VisitInterpolatedStringText(InterpolatedStringTextSyntax node)
        {
            Write(node.TextToken.Text);
        }

        public override void VisitInterpolation(InterpolationSyntax node)
        {
            Write("${");
            Visit(node.Expression);
            Write("}");
        }

        public override void VisitInterpolationAlignmentClause(InterpolationAlignmentClauseSyntax node)
        {
            base.VisitInterpolationAlignmentClause(node);
        }

        public override void VisitInterpolationFormatClause(InterpolationFormatClauseSyntax node)
        {
            base.VisitInterpolationFormatClause(node);
        }

        public override void VisitGlobalStatement(GlobalStatementSyntax node)
        {
            base.VisitGlobalStatement(node);
        }

        public override void VisitBlock(BlockSyntax node)
        {
            Write(" {");
            NewLine();
            _indent++;
            base.VisitBlock(node);
            _indent--;
            IndentWriteLine("}");
        }

        public void VisitInlineBlock(BlockSyntax node)
        {
            Write(" {");
            NewLine();
            _indent++;
            base.VisitBlock(node);
            _indent--;
            IndentWrite("}");
        }


        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            base.VisitLocalDeclarationStatement(node);
        }

        private bool IsMutated(SyntaxNode node)
        {
            var symbol = _model.GetDeclaredSymbol(node);
            return _assignments.ContainsKey(symbol);
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            foreach (var v in node.Variables)
            {
                if (IsMutated(v))
                {
                    IndentWrite("var");
                }
                else
                {
                    IndentWrite("val");
                }

                //Write($" {v.Identifier} : {TranslateType(node.Type)} = ");
                Write($" {v.Identifier} = ");
                Visit(v.Initializer);

                NewLine();
            }
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var arg = GetArgList(node.ParameterList);
            var methodName = ToCamelCase(node.Identifier.Text);
            var ret = TranslateType(node.ReturnType);
            if (ret == "Unit")
            {
                IndentWrite($"fun {methodName} ({arg})");
            }
            else
            {
                IndentWrite($"fun {methodName} ({arg}) : {ret}");
            }

            if (node.Body != null)
            {
                Visit(node.Body);
            }
            else
            {
                Visit(node.ExpressionBody);
                NewLine(); //should maybe be in the arrow expression visit?
            }
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            base.VisitVariableDeclarator(node);
        }

        public override void VisitArrayRankSpecifier(ArrayRankSpecifierSyntax node)
        {
            base.VisitArrayRankSpecifier(node);
        }

        public override void VisitPointerType(PointerTypeSyntax node)
        {
            //Not supported
            base.VisitPointerType(node);
        }

        public override void VisitNullableType(NullableTypeSyntax node)
        {
            base.VisitNullableType(node);
        }

        public override void VisitTupleType(TupleTypeSyntax node)
        {
            base.VisitTupleType(node);
        }

        public override void VisitTupleElement(TupleElementSyntax node)
        {
            base.VisitTupleElement(node);
        }

        public override void VisitOmittedTypeArgument(OmittedTypeArgumentSyntax node)
        {
            base.VisitOmittedTypeArgument(node);
        }

        public override void VisitRefType(RefTypeSyntax node)
        {
            base.VisitRefType(node);
        }

        public override void VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            Write("(");
            Visit(node.Expression);
            Write(")");
        }


        public override void VisitTupleExpression(TupleExpressionSyntax node)
        {
            var count = node.Arguments.Count;
            if (count == 2)
            {
                Write("Pair(");
            }
            else if (count == 3)
            {
                Write("Triple(");
            }
            else
            {
                Write($"NonSupportedTuple{count}(");
            }

            Delimit(node.Arguments, Visit);
            Write(")");
        }

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            Write(node.OperatorToken.Text);
            Visit(node.Operand);
        }

        public override void VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            //Ignore "await"
            Visit(node.Expression);
        }

        public override void VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
        {
            Write(" = ");
            Visit(node.Expression);
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            Visit(node.Left);
            Write($" {node.OperatorToken} ");
            Visit(node.Right);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            Write("if (");
            Visit(node.Condition);
            Write(") ");
            Visit(node.WhenTrue);
            Write(" else ");
            Visit(node.WhenFalse);
        }

        public override void VisitThisExpression(ThisExpressionSyntax node)
        {
            Write("this");
        }

        public override void VisitBaseExpression(BaseExpressionSyntax node)
        {
            Write("super");
            base.VisitBaseExpression(node);
        }

        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            Write(node.ToString());
        }

        public override void VisitMakeRefExpression(MakeRefExpressionSyntax node)
        {
            base.VisitMakeRefExpression(node);
        }

        public override void VisitRefTypeExpression(RefTypeExpressionSyntax node)
        {
            base.VisitRefTypeExpression(node);
        }

        public override void VisitRefValueExpression(RefValueExpressionSyntax node)
        {
            base.VisitRefValueExpression(node);
        }

        public override void VisitCheckedExpression(CheckedExpressionSyntax node)
        {
            base.VisitCheckedExpression(node);
        }

        public override void VisitDefaultExpression(DefaultExpressionSyntax node)
        {
            var t = TranslateDefaultValue(node.Type) ?? "null";
            Write(t);
        }

        public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            var t = TranslateType(node.Type);
            Write(t);
            Write(".javaClass");
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
        }

        public override void VisitAttributeArgument(AttributeArgumentSyntax node)
        {
        }

        public override void VisitNameEquals(NameEqualsSyntax node)
        {
            base.VisitNameEquals(node);
        }

        public override void VisitTypeParameterList(TypeParameterListSyntax node)
        {
            base.VisitTypeParameterList(node);
        }

        public override void VisitAttributeArgumentList(AttributeArgumentListSyntax node)
        {
        }


        public override void VisitDeclarationExpression(DeclarationExpressionSyntax node)
        {
            Write("var ");
            Visit(node.Designation);
            //base.VisitDeclarationExpression(node);
        }

        public override void VisitWhenClause(WhenClauseSyntax node)
        {
            base.VisitWhenClause(node);
        }

        public override void VisitDeclarationPattern(DeclarationPatternSyntax node)
        {
            var t = TranslateType(node.Type);
            Write(t);
            Write(" /* " + node.Designation + "  */");
        }

        public override void VisitDiscardDesignation(DiscardDesignationSyntax node)
        {
            Write("_");
        }

        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            base.VisitForEachVariableStatement(node);
        }

        public override void VisitIsPatternExpression(IsPatternExpressionSyntax node)
        {
            Visit(node.Expression);
            Write(" is ");
            Visit(node.Pattern);
        }

        public override void VisitThrowExpression(ThrowExpressionSyntax node)
        {
            Write("throw ");
            Visit(node.Expression);
        }
    }
}