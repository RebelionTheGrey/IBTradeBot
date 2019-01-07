using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var newIBBot = new WrapperImplementationEx();

            newIBBot.Connect();
            newIBBot.StartMessageProcessing(); 

            Console.ReadLine(); 
        }
    }
}
