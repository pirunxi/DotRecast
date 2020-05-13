using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Perfect.DotRecast
{
    public static class MathUtil
    {

        /// <summary>
        /// 角度 degress 转换为 弧度 radians
        /// </summary>
        /// <param name="degrees"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DegreesToRadians(float degrees)
        {
            return degrees * MathF.PI / 180f;
        }

        /// <summary>
        /// 弧度 radians 转换为 角度
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RadiansToDegrees(float radians)
        {
            return radians * 180f / MathF.PI;
        }

        /// <summary>
        /// 计算 degree 角度 对应 sin 值
        /// </summary>
        /// <param name="degress"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinDegree(float degress)
        {
            return MathF.Sin(DegreesToRadians(degress));
        }

        /// <summary>
        /// 计算 degree 对应 cos 值
        /// </summary>
        /// <param name="degress"></param>
        /// <returns></returns>
        public static float CosDegree(float degress)
        {
            return MathF.Cos(DegreesToRadians(degress));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ConcatHash(int curHash, int appendHash)
        {
            return curHash * 1234232 + appendHash;
        }

        /// <summary>
        /// 计算该三角形的坡度是否小于指定阈值坡度
        /// </summary>
        /// <param name="pa"></param>
        /// <param name="pb"></param>
        /// <param name="pc"></param>
        /// <param name="maxWalkableSlope"> 最大可行走坡度 </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckTriangleSlopeNotExceedSlope(ref Vector3 pa, ref Vector3 pb, ref Vector3 pc, float maxWalkableSlope)
        {
            // 如果超时
            Vector3 ab = pb - pa;
            Vector3 ac = pc - pa;
            Vector3 cross = Vector3.Cross(ab, ac);
            return cross.X * cross.X + cross.Z * cross.Z > cross.Y * cross.Y * maxWalkableSlope * maxWalkableSlope;
        }


        /// <summary>
        ///  计算 由顶点 a,b,c构成的三角形的 包围盒
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="min"> (minX,minY,minZ)</param>
        /// <param name="max">(maxX,maxY,maxZ)</param>
        public static void CalcBounds(ref Vector3 a, ref Vector3 b, ref Vector3 c, out Vector3 min, out Vector3 max)
        {
            float minX = Math.Min(Math.Min(a.X, b.X), c.X);
            float maxX = Math.Max(Math.Max(a.X, b.X), c.X);

            float minY = Math.Min(Math.Min(a.Y, b.Y), c.Y);
            float maxY = Math.Max(Math.Max(a.Y, b.Y), c.Y);

            float minZ = Math.Min(Math.Min(a.Z, b.Z), c.Z);
            float maxZ = Math.Max(Math.Max(a.Z, b.Z), c.Z);

            min = new Vector3(minX, minY, minZ);
            max = new Vector3(maxX, maxY, maxZ);

            /*
            min.X = Math.Min(Math.Min(a.X, b.X), c.X);
            max.X = Math.Max(Math.Max(a.X, b.X), c.X);

            min.Y = Math.Min(Math.Min(a.Y, b.Y), c.Y);
            max.Y = Math.Max(Math.Max(a.Y, b.Y), c.Y);

            min.Z = Math.Min(Math.Min(a.Z, b.Z), c.Z);
            max.Z = Math.Max(Math.Max(a.Z, b.Z), c.Z);
             */
        }

        /// <summary>
        /// 检测 包围盒(minA,maxA) 与 (minB,maxB) 是否相交
        /// </summary>
        /// <param name="minA"></param>
        /// <param name="maxA"></param>
        /// <param name="minB"></param>
        /// <param name="maxB"></param>
        /// <returns>true表示相交</returns>
        public static bool OverlapBounds(ref Vector3 minA, ref Vector3 maxA, ref Vector3 minB, ref Vector3 maxB)
        {
            return minA.X <= maxB.X && minB.X <= maxA.X
                && minA.Y <= maxB.Y && minB.Y <= maxA.Y
                && minA.Z <= maxB.Z && minB.Z <= maxA.Z;
        }
    }
}
