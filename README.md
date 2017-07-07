# ProjectExodus

Transpiler from C# to Kotlin.

Very early pre alpha

Demo:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApplication3
{
    class Example
    {
        public void Main()
        {
            var i = 1.ToString();
            var res = Console.ReadLine();
            Console.WriteLine("You wrote " + res);
        }

        public void Conditionals()
        {
            var x = 1 > 2 ? "a" : "b";
        }
        public string Arrays(string[] strings)
        {
            if (strings == null)
            {
                return "null";
            }
            var x = "";
            foreach (var s in strings)
            {
                x += "," + s;
            }
            return x;
        }

        public void Linq()
        {
            int[] ints = {1, 2, 3, 4, 5, 6, 7, 8};
            var big = ints.Where(i => i > 4).Select(i => i*2).ToList();
        }

        public void Delegates()
        {
            Action<int, string> del = (a, b) =>
            {
                Console.WriteLine("{0} {1}", a, b);
            };
            Func<int, string> del2 = a => "hello" + a;
            InvokeIt(del);
        }

        private void InvokeIt(Action<int, string> del)
        {
            del(1, "hello");
        }
    }
}
```

Gets transpiled into

```kotlin
package consoleapplication3

class Example {
    fun main () : Unit {
        var i : String = 1.toString()
        var res : String = readLine()
        println("You wrote " + res)
    }
    fun conditionals () : Unit {
        var x : String = if (1 > 2) "a" else "b"
    }
    fun arrays (strings : Array<String>) : String {
        if (strings == null) {
            return "null"
        }
        var x : String = ""
        for(s in strings) {
            x += "," + s
        }
        return x
    }
    fun linq () : Unit {
        var ints : Array<Int> = arrayOf(1, 2, 3, 4, 5, 6, 7, 8)
        var big : List<Int> = ints.filter{it > 4}.map{it * 2}.toList()
    }
    fun delegates () : Unit {
        var del : (Int, String) -> Unit = {a, b ->
            println("{0} {1}", a, b)
        }

        var del2 : (Int) -> String = {a -> "hello" + a}
        invokeIt(del)
    }
    fun invokeIt (del : (Int, String) -> Unit) : Unit {
        del(1, "hello")
    }
}
```
