﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Randomizer
{
    public static class RandomGen3
    {
        private static RNGCryptoServiceProvider _global = new RNGCryptoServiceProvider();
        [ThreadStatic]
        private static Random _local;

        public static int Next()
        {
            Random inst = _local;
            if (inst == null)
            {
                byte[] buffer = new byte[4];
                _global.GetBytes(buffer);
                _local = inst = new Random(BitConverter.ToInt32(buffer, 0));
            }

            return inst.Next();
        }
    }
}
