using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proto.Mailbox
{
    static class Foo
    {
        static void Bar() { }

        static void NotOperator()
        {
            if(!true)
            {
                
            }

            var x = (1, 2, true);
        }
    }
}
