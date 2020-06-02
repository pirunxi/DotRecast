using System;
using System.Collections.Generic;
using System.Text;

namespace Perfect.DotRecast.Utils
{
    public static class ValueUtil
    {
        public static void Swap<T>(ref T a, ref T b)
        {
            ref T c = ref a;
            a = b;
            b = c;
        }
    }
}
