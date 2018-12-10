using IBTradeBot.Randomizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Containers
{
    public class BaseContainer
    {
        public int Id { get; set; }
        public string Account { get; set; }

        public BaseContainer() => Id = RandomGen3.Next();
        public BaseContainer(string account) : base() => Account = account;
    }
}
