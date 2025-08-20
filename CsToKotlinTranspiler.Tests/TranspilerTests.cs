using System;
using Xunit;

namespace CsToKotlinTranspiler.Tests;

public class TranspilerTests
{
    [Fact]
    public void TranslatesConsoleWriteLine()
    {
        var code = "using System;\nclass Example { void Main() { Console.WriteLine(\"hello\"); } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("println(\"hello\")", kt);
    }

    [Fact]
    public void TranslatesConsoleReadLine()
    {
        var code = "using System;\nclass Example { void Main() { var res = Console.ReadLine(); } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("readln()", kt);
    }

    [Fact]
    public void TranslatesConditionalOperator()
    {
        var code = "class Example { string Foo() { return 1 > 2 ? \"a\" : \"b\"; } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("if (1 > 2) \"a\" else \"b\"", kt);
    }

    [Fact]
    public void TranslatesArrayAndLoop()
    {
        var code = "class Example { string Join(string[] strings) { var x=\"\"; foreach(var s in strings) { x += \",\" + s; } return x; } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("for(s in strings)", kt);
    }

    [Fact]
    public void TranslatesLinq()
    {
        var code = "using System.Linq;\nclass Example { void Linq() { int[] ints = {1,2,3,4,5}; var big = ints.Where(i => i > 4).Select(i => i * 2).ToList(); } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("ints.filter", kt);
    }

    [Fact]
    public void TranslatesDelegates()
    {
        var code = "using System;\nclass Example { void Delegates() { Action<int,string> del = (a,b)=>{ Console.WriteLine(\"{0} {1}\", a, b); }; del(1,\"x\"); } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("val del = {a, b ->", kt);
    }

    [Fact]
    public void TranslatesProperty()
    {
        var code = "class Example { public string Name { get; set; } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("var name : String", kt);
    }

    [Fact]
    public void TranslatesReadonlyFieldToVal()
    {
        var code = "class Example { private readonly int x = 5; }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("val x : Int = 5", kt);
    }

    [Fact]
    public void TranslatesForLoop()
    {
        var code = "class Example { void Loop(int n) { for(int i = 0; i < n; i++) { var x = i; } } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("for (i in 0 until n)", kt);
    }

    [Fact]
    public void TranslatesWhileLoop()
    {
        var code = "class Example { void Loop() { var i = 0; while(i < 10) { i++; } } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("while (i < 10)", kt);
    }

    [Fact]
    public void TranslatesIfElse()
    {
        var code = "class Example { string Foo(int x) { if(x > 0) { return \"a\"; } else { return \"b\"; } } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("if (x > 0)", kt);
        Assert.Contains("else", kt);
    }

    [Fact]
    public void TranslatesClassAndMethod()
    {
        var code = "class Calculator { public int Add(int a, int b) { return a + b; } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("class Calculator", kt);
        Assert.Contains("fun add", kt);
    }

    [Fact]
    public void TranslatesRecord()
    {
        var code = "public record Person(string Name, int Age);"; // record -> data class
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("data class Person", kt);
        Assert.Contains("val name : String", kt);
        Assert.Contains("val age : Int", kt);
    }

    [Fact]
    public void TranslatesSwitchStatement()
    {
        var code = "class Example { string Foo(int x) { switch(x) { case 1: return \"a\"; default: return \"b\"; } } }"; // switch -> when
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("when (tmp)", kt);
        Assert.Contains("1 ->", kt);
        Assert.Contains("else ->", kt);
    }

    [Fact]
    public void TranslatesTryCatch()
    {
        var code = "using System; class Example { void Foo() { try { Console.WriteLine(\"a\"); } catch(Exception ex) { Console.WriteLine(ex.Message); } } }"; // try/catch preserved
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("try", kt);
        Assert.Contains("catch (ex : Exception)", kt);
    }

    [Fact]
    public void TranslatesDelegateDeclaration()
    {
        var code = "public delegate int Adder(int a, int b);";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("typealias Adder = (Int, Int) -> Int", kt);
    }

    [Fact]
    public void UsingStatementCommented()
    {
        var code = "using System; class Example { void Foo() { using(var d = new Dummy()) { } } class Dummy : IDisposable { public void Dispose() { } } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("/* unsupported: using statement", kt);
        Assert.Contains("using(var d = new Dummy())", kt);
    }

    [Fact]
    public void FixedStatementCommented()
    {
        var code = "unsafe class Example { void Foo(int[] arr) { fixed(int* p = arr) { } } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("/* unsupported: fixed statement", kt);
        Assert.Contains("fixed(int* p = arr)", kt);
    }

    [Fact]
    public void CheckedStatementCommented()
    {
        var code = "class Example { void Foo() { checked { int x = int.MaxValue + 1; } } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("/* unsupported: checked statement", kt);
        Assert.Contains("checked {", kt);
    }

    [Fact]
    public void UnsafeStatementCommented()
    {
        var code = "class Example { void Foo() { unsafe { int x = 0; } } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("/* unsupported: unsafe statement", kt);
        Assert.Contains("unsafe {", kt);
    }

    [Fact]
    public void PointerTypeCommented()
    {
        var code = "unsafe class Example { int* ptr; }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("/* unsupported: pointer type", kt);
        Assert.Contains("int* ptr", kt);
    }
}
