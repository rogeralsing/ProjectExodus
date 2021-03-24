using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharp.CSharpExtensions;

namespace CsToKotlinTranspiler.Translators
{
    internal abstract class MethodTranslator
    {
        public abstract bool Translate(InvocationExpressionSyntax invocation, SemanticModel model);
    }


    internal abstract class MemberAccessExpressionTranslator : MethodTranslator
    {
        protected abstract string Signature();


        public override bool Translate(InvocationExpressionSyntax invocation, SemanticModel model)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax mae)
            {
                var method = mae.Name.Identifier.Text;
                var sym = CSharpExtensions.GetSymbolInfo(model, invocation).Symbol;
                var @class = sym?.ContainingType?.Name;

                if (Signature() != @class + "." + method)
                {
                    return false;
                }

                Translate(model, invocation, mae, @class, method);
                return true;
            }

            return false;
        }

        protected abstract void Translate(SemanticModel model, InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess, string containingTypeName, string methodName);
    }


    internal class ConsoleWriteLine : MemberAccessExpressionTranslator
    {
        protected override string Signature() => "Console.WriteLine";

        protected override void Translate(SemanticModel model, InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess, string containingTypeName, string methodName)
        {
        }
    }
}