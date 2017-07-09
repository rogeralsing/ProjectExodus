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
        private int _indent;

        public KotlinTranspilerVisitor(SemanticModel model, SyntaxWalkerDepth depth = SyntaxWalkerDepth.Node) : base(depth)
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
            Indent($"constructor({arg}) ");
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
            var t = GetKotlinType(node.Type);

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
                Indent("get() = ");
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
                WriteModifiers(node.Modifiers);
                var isReadOnly = FieldIsReadOnly(node);
                Write(isReadOnly ? "val" : "var");
                var t = GetKotlinType(node.Declaration.Type);
                var d = GetKotlinDefaultValue(node.Declaration.Type);
                var nullable = v.Initializer == null && !isReadOnly;
                if (v.Initializer != null)
                {
                    Write($" {v.Identifier} : {t} = ");
                    Visit(v.Initializer.Value);
                }
                else if (d != null)
                {
                    Write($" {v.Identifier} : {t} = {d}");
                }
                else if (nullable)
                {

                    Write($" {v.Identifier} : {t}? = null");
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
            WriteLine($"package {GetKotlinPackageName(node.Name.ToString())}");
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
            NewLine();
            WriteClassModifiers(node.Modifiers);
            Write($"class {node.Identifier}");

            if (node.BaseList != null)
            {
                Write(" : ");
                bool first = true;
                foreach (var t in node.BaseList.Types)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        Write(", ");
                    }
                    var tn = GetKotlinType(t.Type);
                    Write(tn);
                }
                //   var types = node.BaseList.Types.Select(t => _model.GetSymbolInfo(t.Type)).ToArray();
            }

            Write(" {");
            NewLine();
            _indent++;
            var statics = node.Members.Where(mm =>
            {
                if (mm is FieldDeclarationSyntax field)
                {
                    //const fields are companion fields
                    if (field.Modifiers.Any(mod => mod.Text == "const"))
                    {
                        return true;
                    }
                }
                var mmm = _model.GetDeclaredSymbol(mm);
                return mmm?.IsStatic == true;
            }).ToList();
            var instance = node.Members.Except(statics).ToList();

            if (statics.Any())
            {
                WriteLine("companion object {");
                _indent++;
                foreach (var m in statics)
                {
                    Visit(m);
                }
                _indent--;
                WriteLine("}");
            }
            foreach (var m in instance)
            {
                Visit(m);
            }
            _indent--;
            WriteLine("}");
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            base.VisitStructDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            NewLine();
            WriteModifiers(node.Modifiers);
            Write($"interface {node.Identifier} {{");
            NewLine();
            _indent++;
            foreach (var m in node.Members)
            {
                Visit(m);
            }
            _indent--;
            WriteLine("}");
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            base.VisitEnumDeclaration(node);
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            base.VisitDelegateDeclaration(node);
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            base.VisitEnumMemberDeclaration(node);
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
            base.VisitEqualsValueClause(node);
        }

        public override void VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
        {
            base.VisitSingleVariableDesignation(node);
        }

        public override void VisitParenthesizedVariableDesignation(ParenthesizedVariableDesignationSyntax node)
        {
            Write("(");
            bool first = true;
            foreach (SingleVariableDesignationSyntax v in node.Variables)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Write(", ");
                }
                Write(v.Identifier.Text);
            }
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
            base.VisitEmptyStatement(node);
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
            base.VisitBreakStatement(node);
        }

        public override void VisitContinueStatement(ContinueStatementSyntax node)
        {
            base.VisitContinueStatement(node);
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            Indent("return ");
            Visit(node.Expression);
            NewLine();
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            Indent("throw ");
            Visit(node.Expression);
            NewLine();
        }

        public override void VisitYieldStatement(YieldStatementSyntax node)
        {
            base.VisitYieldStatement(node);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            Indent("while (");
            Visit(node.Condition);
            Write(")");
            VisitMaybeBlock(node.Statement);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            var b = node.Statement as BlockSyntax;
            WriteLine("do {");
            _indent++;
            foreach (var s in b.Statements)
            {
                Visit(s);
            }
            _indent--;
            Indent("}");
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
            Indent("for (");
            Write(")");
            VisitMaybeBlock(node.Statement);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            Indent("for(");
            Write(node.Identifier.ToString());
            Write(" in ");
            Visit(node.Expression);
            Write(")");
            VisitMaybeBlock(node.Statement);
        }

        public override void VisitUsingStatement(UsingStatementSyntax node)
        {
            base.VisitUsingStatement(node);
        }

        public override void VisitFixedStatement(FixedStatementSyntax node)
        {
            base.VisitFixedStatement(node);
        }

        public override void VisitCheckedStatement(CheckedStatementSyntax node)
        {
            base.VisitCheckedStatement(node);
        }

        public override void VisitUnsafeStatement(UnsafeStatementSyntax node)
        {
            base.VisitUnsafeStatement(node);
        }

        public override void VisitLockStatement(LockStatementSyntax node)
        {
            base.VisitLockStatement(node);
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            Indent("if (");
            Visit(node.Condition);
            Write(")");
            VisitMaybeBlock(node.Statement);
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

        public override void VisitElseClause(ElseClauseSyntax node)
        {
            base.VisitElseClause(node);
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            Indent("val tmp = ");
            Visit(node.Expression);
            NewLine();
            WriteLine("when (tmp) {");
            _indent++;
            foreach (var s in node.Sections)
            {
                Visit(s);
            }
            _indent--;
            WriteLine("}");
        }

        public override void VisitSwitchSection(SwitchSectionSyntax node)
        {
            if (node.Statements.Count > 1)
            {
                Indent("is ");
                var c = node.Labels.First() as CasePatternSwitchLabelSyntax;
                var d = c.Pattern as DeclarationPatternSyntax;
                var v = d.Designation as SingleVariableDesignationSyntax;

                var t = GetKotlinType(d.Type);
                Write(t);
                Write(" -> {");
                NewLine();
                _indent++;
                WriteLine($"val {v.Identifier.Text} = tmp");
                foreach (var s in node.Statements)
                {
                    Visit(s);
                }
                _indent--;
                WriteLine("}");
            }
            else
            {
                Indent(" -> ");
                var i = _indent;
                _indent = 0;
                Visit(node.Statements.First());
                _indent = i;
                NewLine();
            }
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
            Indent("try ");
            Visit(node.Block);
            foreach (var c in node.Catches)
            {
                var v = c.Declaration.Identifier.Text;
                var t = GetKotlinType(c.Declaration.Type);
                Indent($"catch ({v} : {t})");
                Visit(c.Block);
            }
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            base.VisitCatchClause(node);
        }

        public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
        {
            base.VisitCatchDeclaration(node);
        }

        public override void VisitCatchFilterClause(CatchFilterClauseSyntax node)
        {
            base.VisitCatchFilterClause(node);
        }

        public override void VisitFinallyClause(FinallyClauseSyntax node)
        {
            base.VisitFinallyClause(node);
        }

        public override void VisitCompilationUnit(CompilationUnitSyntax node)
        {
            base.VisitCompilationUnit(node);
        }

        public override void VisitExternAliasDirective(ExternAliasDirectiveSyntax node)
        {
            base.VisitExternAliasDirective(node);
        }

        public override void VisitSizeOfExpression(SizeOfExpressionSyntax node)
        {
            base.VisitSizeOfExpression(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);
        }

        public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            base.VisitElementAccessExpression(node);
        }

        //public override void VisitIdentifierName(IdentifierNameSyntax node)
        //{
        //    if (node.Identifier.ToString() == "ToLowerInvariant")
        //    {

        //    }
        //    base.VisitIdentifierName(node);
        //}

        public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
        {
            base.VisitPostfixUnaryExpression(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var methodName = node.Name.ToString();
            var sym = _model.GetSymbolInfo(node).Symbol;
            var containingTypeName = sym?.ContainingType?.Name;

            switch (containingTypeName)
            {
                case nameof(Enumerable):
                    switch (methodName)
                    {
                        case nameof(Enumerable.Select):
                            Visit(node.Expression);
                            Write(".");
                            Write("map");
                            break;
                        case nameof(Enumerable.Where):
                            Visit(node.Expression);
                            Write(".");
                            Write("filter");
                            break;
                        case nameof(Enumerable.ToList):
                            Visit(node.Expression);
                            Write(".");
                            Write("toList");
                            break;
                        default:
                            break;
                    }
                    break;
                case nameof(Console):
                    switch (methodName)
                    {
                        case nameof(Console.WriteLine):
                            Write("println");
                            break;
                        case nameof(Console.Write):
                            Write("print");
                            break;
                        case nameof(Console.ReadLine):
                            Write("readLine");
                            break;
                    }
                    break;
                default:
                    Visit(node.Expression);
                    Write(".");
                    var name = node.Name.ToString();
                    if (sym.Kind == SymbolKind.Field)
                    {
                        //pass
                    }
                    else
                    {
                        name = ToCamelCase(name);
                    }
                   
                    Write(name);
                    break;
            }

            //  base.VisitMemberAccessExpression(node);
        }

        public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            base.VisitConditionalAccessExpression(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var arg = GetArgList(node.ParameterList);
            var methodName = ToCamelCase(node.Identifier.Text);
            var ret = GetKotlinType(node.ReturnType);
            WriteModifiers(node.Modifiers);

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

        private void WriteClassModifiers(SyntaxTokenList mods)
        {
            var modifiers = mods.Select(m => m.ToString()).ToImmutableHashSet();

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

        private void WriteModifiers(SyntaxTokenList mods)
        {
            var modifiers = mods.Select(m => m.ToString()).ToImmutableHashSet();

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

        public override void VisitToken(SyntaxToken token)
        {
            base.VisitToken(token);
        }

        public override void VisitLeadingTrivia(SyntaxToken token)
        {
            base.VisitLeadingTrivia(token);
        }

        public override void VisitTrailingTrivia(SyntaxToken token)
        {
            base.VisitTrailingTrivia(token);
        }

        public override void VisitTrivia(SyntaxTrivia trivia)
        {
            base.VisitTrivia(trivia);
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

        public override void VisitQualifiedName(QualifiedNameSyntax node)
        {
            base.VisitQualifiedName(node);
        }

        public override void VisitGenericName(GenericNameSyntax node)
        {
            base.VisitGenericName(node);
        }

        public override void VisitTypeArgumentList(TypeArgumentListSyntax node)
        {
            base.VisitTypeArgumentList(node);
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
            Write(node.OperatorToken.Text);
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

        public override void VisitTypeCref(TypeCrefSyntax node)
        {
            base.VisitTypeCref(node);
        }

        public override void VisitQualifiedCref(QualifiedCrefSyntax node)
        {
            base.VisitQualifiedCref(node);
        }

        public override void VisitNameMemberCref(NameMemberCrefSyntax node)
        {
            base.VisitNameMemberCref(node);
        }

        public override void VisitIndexerMemberCref(IndexerMemberCrefSyntax node)
        {
            base.VisitIndexerMemberCref(node);
        }

        public override void VisitOperatorMemberCref(OperatorMemberCrefSyntax node)
        {
            base.VisitOperatorMemberCref(node);
        }

        public override void VisitConversionOperatorMemberCref(ConversionOperatorMemberCrefSyntax node)
        {
            base.VisitConversionOperatorMemberCref(node);
        }

        public override void VisitCrefParameterList(CrefParameterListSyntax node)
        {
            base.VisitCrefParameterList(node);
        }

        public override void VisitCrefBracketedParameterList(CrefBracketedParameterListSyntax node)
        {
            base.VisitCrefBracketedParameterList(node);
        }

        public override void VisitCrefParameter(CrefParameterSyntax node)
        {
            base.VisitCrefParameter(node);
        }

        public override void VisitXmlElement(XmlElementSyntax node)
        {
            base.VisitXmlElement(node);
        }

        public override void VisitXmlElementStartTag(XmlElementStartTagSyntax node)
        {
            base.VisitXmlElementStartTag(node);
        }

        public override void VisitXmlElementEndTag(XmlElementEndTagSyntax node)
        {
            base.VisitXmlElementEndTag(node);
        }

        public override void VisitXmlEmptyElement(XmlEmptyElementSyntax node)
        {
            base.VisitXmlEmptyElement(node);
        }

        public override void VisitXmlName(XmlNameSyntax node)
        {
            base.VisitXmlName(node);
        }

        public override void VisitXmlPrefix(XmlPrefixSyntax node)
        {
            base.VisitXmlPrefix(node);
        }

        public override void VisitXmlTextAttribute(XmlTextAttributeSyntax node)
        {
            base.VisitXmlTextAttribute(node);
        }

        public override void VisitXmlCrefAttribute(XmlCrefAttributeSyntax node)
        {
            base.VisitXmlCrefAttribute(node);
        }

        public override void VisitXmlNameAttribute(XmlNameAttributeSyntax node)
        {
            base.VisitXmlNameAttribute(node);
        }

        public override void VisitXmlText(XmlTextSyntax node)
        {
            base.VisitXmlText(node);
        }

        public override void VisitXmlCDataSection(XmlCDataSectionSyntax node)
        {
            base.VisitXmlCDataSection(node);
        }

        public override void VisitXmlProcessingInstruction(XmlProcessingInstructionSyntax node)
        {
            base.VisitXmlProcessingInstruction(node);
        }

        public override void VisitXmlComment(XmlCommentSyntax node)
        {
            base.VisitXmlComment(node);
        }

        public override void VisitIfDirectiveTrivia(IfDirectiveTriviaSyntax node)
        {
            base.VisitIfDirectiveTrivia(node);
        }

        public override void VisitElifDirectiveTrivia(ElifDirectiveTriviaSyntax node)
        {
            base.VisitElifDirectiveTrivia(node);
        }

        public override void VisitElseDirectiveTrivia(ElseDirectiveTriviaSyntax node)
        {
            base.VisitElseDirectiveTrivia(node);
        }

        public override void VisitEndIfDirectiveTrivia(EndIfDirectiveTriviaSyntax node)
        {
            base.VisitEndIfDirectiveTrivia(node);
        }

        public override void VisitRegionDirectiveTrivia(RegionDirectiveTriviaSyntax node)
        {
            base.VisitRegionDirectiveTrivia(node);
        }

        public override void VisitEndRegionDirectiveTrivia(EndRegionDirectiveTriviaSyntax node)
        {
            base.VisitEndRegionDirectiveTrivia(node);
        }

        public override void VisitErrorDirectiveTrivia(ErrorDirectiveTriviaSyntax node)
        {
            base.VisitErrorDirectiveTrivia(node);
        }

        public override void VisitWarningDirectiveTrivia(WarningDirectiveTriviaSyntax node)
        {
            base.VisitWarningDirectiveTrivia(node);
        }

        public override void VisitBadDirectiveTrivia(BadDirectiveTriviaSyntax node)
        {
            base.VisitBadDirectiveTrivia(node);
        }

        public override void VisitDefineDirectiveTrivia(DefineDirectiveTriviaSyntax node)
        {
            base.VisitDefineDirectiveTrivia(node);
        }

        public override void VisitUndefDirectiveTrivia(UndefDirectiveTriviaSyntax node)
        {
            base.VisitUndefDirectiveTrivia(node);
        }

        public override void VisitLineDirectiveTrivia(LineDirectiveTriviaSyntax node)
        {
            base.VisitLineDirectiveTrivia(node);
        }

        public override void VisitPragmaWarningDirectiveTrivia(PragmaWarningDirectiveTriviaSyntax node)
        {
            base.VisitPragmaWarningDirectiveTrivia(node);
        }

        public override void VisitPragmaChecksumDirectiveTrivia(PragmaChecksumDirectiveTriviaSyntax node)
        {
            base.VisitPragmaChecksumDirectiveTrivia(node);
        }

        public override void VisitReferenceDirectiveTrivia(ReferenceDirectiveTriviaSyntax node)
        {
            base.VisitReferenceDirectiveTrivia(node);
        }

        public override void VisitLoadDirectiveTrivia(LoadDirectiveTriviaSyntax node)
        {
            base.VisitLoadDirectiveTrivia(node);
        }

        public override void VisitShebangDirectiveTrivia(ShebangDirectiveTriviaSyntax node)
        {
            base.VisitShebangDirectiveTrivia(node);
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
            base.VisitCastExpression(node);
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            base.VisitAnonymousMethodExpression(node);
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
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
                WriteLine("}");
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
            Write("{");
            var arg = GetArgList(node.ParameterList);
            Write(arg);
            Write(" -> ");
            if (node.Body is BlockSyntax block)
            {
                NewLine();
                _indent++;
                foreach (var s in block.Statements)
                {
                    Visit(s);
                }
                _indent--;
                WriteLine("}");
            }
            else
            {
                Visit(node.Body);
                Write("}");
            }
        }

        public override void VisitInitializerExpression(InitializerExpressionSyntax node)
        {
            void Init(string sep)
            {
                var first = true;
                foreach (var e in node.Expressions)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        Write(sep);
                    }
                    Visit(e);
                }
            }

            if (node.Parent is ObjectCreationExpressionSyntax parent)
            {
                var t = _model.GetSymbolInfo(parent.Type).Symbol;
                if (t?.Name == nameof(List<object>))
                {
                    Write("listOf(");
                    Init(", ");
                    Write(")");
                    return;
                }
                else
                {
                    Write("().apply {");
                    Init("; ");
                    Write("}");
                    return;
                }
            }

            Write("arrayOf(");
            Init(", ");
            Write(")");
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var t = GetKotlinType(node.Type);
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
            bool first = true;
            foreach (var a in node.Arguments)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Write(", ");
                }
                Visit(a);
            }
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
                if (sym != null && sym.ToString() == "lambda expression") //TODO: I have no idea how to check this correctly
                {
                    Visit(arg);
                    return;
                }
            }

            Write("(");
            var first = true;
            foreach (var a in node.Arguments)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Write(", ");
                }
                Visit(a);
            }
            Write(")");
        }

        public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
        {
            base.VisitArrayCreationExpression(node);
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
            WriteLine("}");
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            base.VisitLocalDeclarationStatement(node);
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            foreach (var v in node.Variables)
            {
                Indent($"var {v.Identifier} : {GetKotlinType(node.Type)} = ");
                Visit(v.Initializer);

                NewLine();
            }
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var arg = GetArgList(node.ParameterList);
            var methodName = ToCamelCase(node.Identifier.Text);
            var ret = GetKotlinType(node.ReturnType);
            if (ret == "Unit")
            {
                Indent($"fun {methodName} ({arg})");
            }
            else
            {
                Indent($"fun {methodName} ({arg}) : {ret}");
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
            base.VisitParenthesizedExpression(node);
        }

        public override void VisitTupleExpression(TupleExpressionSyntax node)
        {
            base.VisitTupleExpression(node);
        }

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            base.VisitPrefixUnaryExpression(node);
        }

        public override void VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            base.VisitAwaitExpression(node);
        }

        public override void VisitArrayType(ArrayTypeSyntax node)
        {
            base.VisitArrayType(node);
        }

        public override void VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
        {
            Write(" = ");
            Visit(node.Expression);
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            base.VisitEventDeclaration(node);
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
            base.VisitDefaultExpression(node);
        }

        public override void VisitTypeOfExpression(TypeOfExpressionSyntax node)
        {
            base.VisitTypeOfExpression(node);
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            base.VisitAttribute(node);
        }

        public override void VisitAttributeArgument(AttributeArgumentSyntax node)
        {
            base.VisitAttributeArgument(node);
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
            base.VisitAttributeArgumentList(node);
        }

        public override void VisitCasePatternSwitchLabel(CasePatternSwitchLabelSyntax node)
        {
            base.VisitCasePatternSwitchLabel(node);
        }

        public override void VisitConstantPattern(ConstantPatternSyntax node)
        {
            base.VisitConstantPattern(node);
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
            base.VisitDeclarationPattern(node);
        }

        public override void VisitDiscardDesignation(DiscardDesignationSyntax node)
        {
            base.VisitDiscardDesignation(node);
        }

        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            base.VisitForEachVariableStatement(node);
        }

        public override void VisitIsPatternExpression(IsPatternExpressionSyntax node)
        {
            base.VisitIsPatternExpression(node);
        }

        public override void VisitThrowExpression(ThrowExpressionSyntax node)
        {
            Write("throw ");
            Visit(node.Expression);
        }
    }
}