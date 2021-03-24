using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsToKotlinTranspiler
{
    public partial class KotlinTranspilerVisitor
    {
        private readonly Dictionary<string, Action<InvocationExpressionSyntax, MemberAccessExpressionSyntax>>
            _methodTranslators = new();

        public void Translate(string signature, Action<InvocationExpressionSyntax, MemberAccessExpressionSyntax> body)
        {
            _methodTranslators.Add(signature, body);
        }

        public void Setup()
        {
            Translate("Console.WriteLine", (node, member) =>
            {
                Write("println");
                Visit(node.ArgumentList);
            });
            Translate("Console.Write", (node, member) =>
            {
                Write("print");
                Visit(node.ArgumentList);
            });
            Translate("Console.ReadLine", (node, member) =>
            {
                Write("readln");
                Visit(node.ArgumentList);
            });
            Translate("PID.Tell", (node, member) =>
            {
                Visit(member.Expression);
                Write(".send");
                Visit(node.ArgumentList);
            });
            Translate("IContext.Tell", (node, member) =>
            {
                Visit(member.Expression);
                Write(".send");
                Visit(node.ArgumentList);
            });
            Translate("PID.RequestAsync", (node, member) =>
            {
                Visit(member.Expression);
                Write(".requestAwait");
                Visit(node.ArgumentList);
            });
            Translate("IContext.RequestAsync", (node, member) =>
            {
                Visit(member.Expression);
                Write(".requestAwait");
                Visit(node.ArgumentList);
            });
            Translate("IContext.Spawn", (node, member) =>
            {
                Visit(member.Expression);
                Write(".spawnChild");
                Visit(node.ArgumentList);
            });
            Translate("IContext.SpawnNamed", (node, member) =>
            {
                Visit(member.Expression);
                Write(".spawnNamedChild");
                Visit(node.ArgumentList);
            });
            Translate("IContext.SpawnPrefix", (node, member) =>
            {
                Visit(member.Expression);
                Write(".spawnPrefixChild");
                Visit(node.ArgumentList);
            });
            Translate("TimeSpan.FromSeconds", (node, member) =>
            {
                Write("Duration.ofSeconds");
                Visit(node.ArgumentList);
            });
            Translate("TimeSpan.FromMilliseconds", (node, member) =>
            {
                Write("Duration.ofMillis");
                Visit(node.ArgumentList);
            });
            Translate("Assert.Equal", (node, member) =>
            {
                Write("assertEquals");
                Visit(node.ArgumentList);
            });
            Translate("Assert.NotEqual", (node, member) =>
            {
                Write("assertNotEquals");
                Visit(node.ArgumentList);
            });
            Translate("Assert.Same", (node, member) =>
            {
                Write("assertSame");
                Visit(node.ArgumentList);
            });
            Translate("Assert.True", (node, member) =>
            {
                Write("assertTrue");
                Visit(node.ArgumentList);
            });
            Translate("Assert.False", (node, member) =>
            {
                Write("assertFalse");
                Visit(node.ArgumentList);
            });
            Translate("Assert.Contains", (node, member) =>
            {
                Write("assertTrue (");
                var element = node.ArgumentList.Arguments.First();
                var collection = node.ArgumentList.Arguments.Last();
                Visit(collection);
                Write(".contains(");
                Visit(element);
                Write("))");
            });
            Translate("Assert.DoesNotContain", (node, member) =>
            {
                Write("assertFalse (");
                var element = node.ArgumentList.Arguments.First();
                var collection = node.ArgumentList.Arguments.Last();
                Visit(collection);
                Write(".contains(");
                Visit(element);
                Write("))");
            });
            Translate("Assert.ThrowsAsync", (node, member) =>
            {
                var n = member.Name as GenericNameSyntax;
                var genericArg = n.TypeArgumentList.Arguments.First();
                var t = TranslateType(genericArg);
                Write($"assertFailsWith<{t}>");
                Visit(node.ArgumentList);
            });
            Translate("Assert.IsType", (node, member) =>
            {
                if (node.ArgumentList.Arguments.Count() == 1)
                {
                    Write("assertTrue (");
                    var element = member.Name as GenericNameSyntax;
                    var t = TranslateType(element.TypeArgumentList.Arguments.First());
                    var collection = node.ArgumentList.Arguments.Last();
                    Visit(collection);
                    Write($" is {t})");
                }

                if (node.ArgumentList.Arguments.Count == 2)
                {
                    Write("assertTrue (");
                    var element = node.ArgumentList.Arguments.First().Expression as TypeOfExpressionSyntax;
                    var t = TranslateType(element.Type);
                    var collection = node.ArgumentList.Arguments.Last();
                    Visit(collection);
                    Write($" is {t})");
                }
            });
            Translate("Assert.Null", (node, member) =>
            {
                Write("assertNull");
                Visit(node.ArgumentList);
            });
            Translate("Assert.NotNull", (node, member) =>
            {
                Write("assertNotNull");
                Visit(node.ArgumentList);
            });

            Translate("EventWaitHandle.Set", (node, member) =>
            {
                Visit(member.Expression);
                Write(".countDown()");
            });
            Translate("Enumerable.Where", (node, member) =>
            {
                Visit(member.Expression);
                Write(".");
                Write("filter");
                Visit(node.ArgumentList);
            });
            Translate("Enumerable.Select", (node, member) =>
            {
                Visit(member.Expression);
                Write(".");
                Write("map");
                Visit(node.ArgumentList);
            });
            Translate("Enumerable.ToList", (node, member) =>
            {
                Visit(member.Expression);
                Write(".");
                Write("toList");
                Visit(node.ArgumentList);
            });
            Translate("Enumerable.ToArray", (node, member) =>
            {
                Visit(member.Expression);
                Write(".");
                Write("toTypedArray");
                Visit(node.ArgumentList);
            });
            Translate("Enumerable.Concat", (node, member) =>
            {
                var a = node.ArgumentList.Arguments.First();

                Write("(");
                Visit(member.Expression);
                Write(" + ");
                Visit(a);
                Write(")");
            });
            Translate("Enumerable.Range", (node, member) =>
            {
                var start = node.ArgumentList.Arguments.First();
                var count = node.ArgumentList.Arguments.Last();

                if (start.Expression is LiteralExpressionSyntax lit && lit.Token.ValueText == "0")
                {
                    Write("(");
                    Visit(start);
                    Write(" upto ");
                    Visit(count);
                    Write(")");
                }
                else
                {
                    Write("(");
                    Visit(start);
                    Write(" upto ");
                    Visit(start);
                    Write("+");
                    Visit(count);
                    Write(")");
                }
            });
            Translate("Task.FromResult", (node, member) => { Write(""); });
        }
    }
}