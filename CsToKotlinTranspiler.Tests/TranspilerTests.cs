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

    [Fact(Skip = "LINQ translation not implemented")]
    public void TranslatesLinq()
    {
        var code = "using System.Linq;\nclass Example { void Linq() { int[] ints = {1,2,3,4,5}; var big = ints.Where(i => i > 4).Select(i => i * 2).ToList(); } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("ints.filter", kt);
    }

    [Fact(Skip = "Delegate translation not implemented")]
    public void TranslatesDelegates()
    {
        var code = "using System;\nclass Example { void Delegates() { Action<int,string> del = (a,b)=>{ Console.WriteLine(\"{0} {1}\", a, b); }; del(1,\"x\"); } }";
        var kt = KotlinTranspiler.Transpile(code);
        Assert.Contains("(Int, String) -> Unit", kt);
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
}
