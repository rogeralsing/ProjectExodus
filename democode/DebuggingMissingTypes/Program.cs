using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebuggingMissingTypes
{
    public interface IFoo
    {
        Task ReceiveAsync();
        void Escalate(string arg1, Exception arg2);
    }

    class Program
    {
        static void Main(string[] args)
        {
        }
    }
}
