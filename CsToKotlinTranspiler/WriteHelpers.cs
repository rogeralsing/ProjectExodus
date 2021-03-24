// -----------------------------------------------------------------------
//   <copyright file="WriteHelpers.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Text;

namespace CsToKotlinTranspiler
{
    public partial class KotlinTranspilerVisitor
    {
        private readonly StringBuilder _sb = new();
        private int _indent;

        private void IndentWrite(string text)
        {
            Write(GetIndent() + text);
        }

        private void Indent()
        {
            Write(GetIndent());
        }

        private void Write(string text)
        {
            if (text == "fromProducer")
            {
            }

            Console.Write(text);
            _sb.Append(text);
        }

        private void NewLine()
        {
            Write("\n");
        }

        private void IndentWriteLine(string text)
        {
            Write(GetIndent() + text);
            NewLine();
        }

        private string GetIndent() => new(' ', _indent * 4);
    }
}