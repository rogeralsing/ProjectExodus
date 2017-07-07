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

        public void LocalMethod()
        {
            string InternalFunc()
            {
                return "result";
            }

            var y = InternalFunc();
        }

        private void InvokeIt(Action<int, string> del)
        {
            del(1, "hello");
        }
    }
}
