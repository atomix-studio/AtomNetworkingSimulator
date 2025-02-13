﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Helpers
{
    public static class NodeRandom
    {
        private static Random _random = new Random();

        public static long UniqueID()
        {
            return _random.Next(-int.MaxValue, int.MaxValue) * _random.Next(-int.MaxValue, int.MaxValue);
        }

        public static float Range(float min, float max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        public static int Range(int min, int max)
        {
            return UnityEngine.Random.Range(min, max);
        }
    }
}
