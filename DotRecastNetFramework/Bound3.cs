using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;
using Perfect.DotRecast.Utils;

namespace Perfect.DotRecast
{
    public struct Bound3 : IEquatable<Bound3>
    {
        public Vector3 MinBound { get; set; }

        public Vector3 MaxBound { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Bound3 b)
            {
                return MinBound == b.MinBound && MaxBound == b.MaxBound;
            }
            else
            {
                return false;
            }
        }

        public bool Equals(Bound3 b)
        {
            return MinBound == b.MinBound && MaxBound == b.MaxBound;
        }

        public override int GetHashCode()
        {
            return MathUtil.ConcatHash(MinBound.GetHashCode(), MaxBound.GetHashCode());
        }

        public static bool operator ==(Bound3 left, Bound3 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Bound3 left, Bound3 right)
        {
            return !(left == right);
        }
    }
}
