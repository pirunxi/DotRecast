using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using Perfect.DotRecast.Rasterzation;
using Perfect.DotRecast.Utils;

namespace Perfect.DotRecast
{

    /// <summary>
    /// 体素化算法
    /// </summary>
    public enum ERasterzationAlgorithm
    {
        RECAST_RASTERZATION, // recast 使用的 体素化算法
        MY_ALGO_1, // 我们使用算法
    }

    /// <summary>
    /// 体素柱中连续Voxel的 紧凑表示
    /// [MinY, MaxY] 表示这段连续Span
    /// </summary>
    public struct VoxelHeightSpan
    {
        /// <summary>
        /// 底部 y 值. 也即 bottomY
        /// </summary>
        public int MinY { get; set; }

        /// <summary>
        /// 顶部 y 值 (包含). 也即 topY
        /// </summary>
        public int MaxY { get; set; }

        /// <summary>
        /// 该 span的arae id.
        /// </summary>
        public int Area { get; set; }

        public int BottomY => MinY;

        public int TopY => MaxY;

        public int Height => MaxY - MinY + 1;


        public VoxelHeightSpan(int miny, int maxy, int area)
        {
            MinY = miny;
            MaxY = maxy;
            Area = area;
        }
    }

    /// <summary>
    /// 相同 xz cell 坐标的 voxel 形成 体素柱.
    /// 将体素柱中连续的体素用 VoxelHeightSpan 紧凑表示
    /// </summary>
    public struct VoxelPillar
    {
        public List<VoxelHeightSpan> Spans { get; set; }
    }

    /// <summary>
    /// 体素场
    /// 对应 recast 里的 rcHeightField
    /// 以 xz 为水平平面. y 为 垂直平面 构建 体素场.
    /// 所有三角形按照 X,Y,Z CellSize 体素化.
    /// 最终形成 XWidth * ZWidth 个 体素柱.
    /// 每个体素柱包含 多个不相交的 VoxcelHeightSpan.
    ///
    /// 使用了 左手坐标系，与unreal和unity一致
	///  三角形顶点方向为顺时针方向。
    /// 但 recast 使用右手坐标系，三角形顶点方向为逆时针方向
    /// </summary>
    public class VoxelField
    {

        private Vector3 _minBound;
        private Vector3 _maxBound;

        public Vector3 MinBound => _minBound;

        public Vector3 MaxBound => _maxBound;

        public ref Vector3 RefMinBound => ref _minBound;

        public ref Vector3 RefMaxBound => ref _maxBound;

        /// <summary>
        /// x 轴方向的 cell 个数. 对应 rcHeightField.width
        /// </summary>
        public int XWidth { get; }

        /// <summary>
        /// z 轴方向的 cell 个数. 对应 rcHeightField.height
        /// </summary>
        public int ZWidth { get; }

        /// <summary>
        ///  x 轴 cell size. 
        /// </summary>
        public float XCellSize { get; }

        /// <summary>
        /// z 轴 cell size.
        /// recast 里 XCellSize == ZCellSize，而我们的实现没这个限制.不过意义不大
        /// </summary>
        public float ZCellSize { get; }

        /// <summary>
        /// y 轴方向  cell height size
        /// </summary>
        public float YCellSize { get; }


#pragma warning disable CA1819 // Properties should not return arrays
        private VoxelPillar[,] VoxelPillarXZArray { get; }
#pragma warning restore CA1819 // Properties should not return arrays


        /// <summary>
        /// 注意，参数中 x,z cellsize 在 y cell size 之前
        /// </summary>
        /// <param name="xWidth"> x 轴 cell 个数</param>
        /// <param name="zWidth"> z 轴 cell 个数</param>
        /// <param name="minBound">包围盒 最小顶点(minX,minY,minZ)</param>
        /// <param name="maxBound">包围盒 最大顶点(maxX,maxY,maxZ)</param>
        /// <param name="xCellSize">x 轴 cell size</param>
        /// <param name="zCellSize">z 轴 cell size</param>
        /// <param name="yCellSize">y 轴 cell size</param>
        public VoxelField(int xWidth, int zWidth, Vector3 minBound, Vector3 maxBound, float xCellSize, float zCellSize, float yCellSize)
        {
            Debug.Assert(xWidth > 0 && zWidth > 0);
            Debug.Assert(xCellSize > 0 && yCellSize > 0 && zCellSize > 0);
            Debug.Assert(minBound.X < MaxBound.X && minBound.Y < MaxBound.Y && minBound.Z < MaxBound.Z);

            XWidth = xWidth;
            ZWidth = zWidth;
            XCellSize = xCellSize;
            ZCellSize = zCellSize;
            YCellSize = yCellSize;

            _minBound = minBound;
            _maxBound = maxBound;

            VoxelPillarXZArray = new VoxelPillar[xWidth, zWidth];
        }

        /// <summary>
        /// 体素化 三角形集.
        /// </summary>
        /// <param name="compactTriangleSet"> 需要被体素化的 三角形集合</param>
        /// <param name="walkableSlopDegree"> 能够攀爬的最大斜坡角度.[0,90] 0表示平面,90度表示垂直地面</param>
        /// <param name="minSpanMergeThreshold"> 当两个span相交时，如果它们的最大高度相差不超过 minSpanMergeThreshold,合并它们  </param>
        public void RasterizeTriangles(MarkedTriangleSet compactTriangleSet, float walkableSlopeDegree, int minSpanMergeThreshold, ERasterzationAlgorithm algorithm)
        {
            float walkableSlope = (float)Math.Cos(walkableSlopeDegree * Math.PI / 180f);

            foreach (MarkedTriangle tri in compactTriangleSet.Triangles)
            {
                // 如果坡度超过可攀爬坡度，标记为 不可行走
                int area = MathUtil.CheckTriangleSlopeNotExceedSlope(ref tri.A, ref tri.B, ref tri.C, walkableSlope) ? tri.Area : Consts.NOT_WALKABLE_AREA;
                RasterizeTriangle_Recast(ref tri.A, ref tri.B, ref tri.C, area, minSpanMergeThreshold);
            }
        }


        private readonly Vector3[] _cacheBuf1 = new Vector3[7];
        private readonly Vector3[] _cacheBuf2 = new Vector3[7];
        private readonly Vector3[] _cacheBuf3 = new Vector3[7];
        private readonly Vector3[] _cacheBuf4 = new Vector3[7];
        private readonly Vector3[] _cacheBuf5 = new Vector3[7];


        private void DividePolyByXAlix(Vector3[] inPoly, int inVertNum, Vector3[] outRowPoly, out int outRowVertNum,
            Vector3[] remainPoly, out int outRemainPolyVertNum, float splitX)
        {
            float[] deltaXs = _cacheFloats;
            for (int i = 0; i < inVertNum; i++)
            {
                deltaXs[i] = splitX - inPoly[i].X;
            }

            int rowVertexNum = 0;
            int remainPolyVertexNum = 0;

            for (int index = 0, prevIndex = inVertNum - 1; index < inVertNum; index++)
            {
                Vector3 curVertex = inPoly[index];
                // 检查一条边的两个顶点是否在 分割线的两侧, 以更小为左侧，以更大为右侧
                bool leftOfSplit1 = deltaXs[index] >= 0f;
                bool leftOfSplit2 = deltaXs[prevIndex] >= 0f;

                float curDeltaX = deltaXs[index];
                float prevDeltaX = deltaXs[prevIndex];
                if (leftOfSplit1 != leftOfSplit2)
                {
                    float splitWeight = prevDeltaX / (prevDeltaX - curDeltaX);
                    Vector3 newSplitVertex = Vector3.Lerp(inPoly[prevIndex], curVertex, splitWeight);
                    outRowPoly[rowVertexNum++] = newSplitVertex;
                    remainPoly[remainPolyVertexNum++] = newSplitVertex;

                    if (curDeltaX > 0)
                    {
                        outRowPoly[rowVertexNum++] = curVertex;
                    }
                    else if (curDeltaX < 0)
                    {
                        remainPoly[remainPolyVertexNum++] = curVertex;
                    }
                    // else curDeltaX 不需要处理。因为此时它等于newSplitVertex, 已经在上面添加了
                }
                else
                {
                    // 这儿略微重构了 recast 的写法
                    // 它的代码有点令人困惑
                    if (curDeltaX > 0)
                    {
                        outRowPoly[rowVertexNum++] = curVertex;
                    }
                    else if (curDeltaX == 0)
                    {
                        outRowPoly[rowVertexNum++] = curVertex;
                        remainPoly[remainPolyVertexNum++] = curVertex;
                    }
                    else
                    {
                        remainPoly[remainPolyVertexNum++] = curVertex;
                    }
                }
            }

            outRowVertNum = rowVertexNum;
            outRemainPolyVertNum = remainPolyVertexNum;
        }


        private readonly float[] _cacheFloats = new float[12];

        /// <summary>
        /// 将输入的多边形以 splitZ 为界，分割为两个多边形
        /// 最原始的 inPoly 是三角形，因此沿着 z 分割最多产生3 + 2 = 5个顶点。
        /// 再接着沿x轴分割时，最多产生 5 + 2 = 7个顶点。也是为什么 _cacheBuff* 长度为 7
        /// 由于rasterzation 沿着 z,x坐标各分割一次.
        /// </summary>
        /// <param name="inPoly"></param>
        /// <param name="inVertNum"></param>
        /// <param name="outRowPoly">分割出的“条”。由于从最初的三角形迭代分割，所以outRowPoly都是长条形的 </param>
        /// <param name="outRowVertNum"></param>
        /// <param name="remainPoly"></param>
        /// <param name="outRemainPolyVertNum"></param>
        /// <param name="splitZ"> 分割线.沿着z=splitZ将inPoly分割成两个多边形 </param>
        private void DividePolyByZAlix(Vector3[] inPoly, int inVertNum, Vector3[] outRowPoly, out int outRowVertNum,
            Vector3[] remainPoly, out int outRemainPolyVertNum, float splitZ)
        {
            //         | /\|
            //         |/  |\
            //      | /|   | \
            //      |/ |   |  \
            //     /|  |   |   \
            //    / |  |   |    \
            //   /__|__|___|__ __\
            //      |  |   |
            //
            // 如上图的方式进行切分，每次切割出“一条”
            // 根据 三角形的情况，每“条” 最多 5个顶点
            // 再 沿另一个轴的方向切割后，每“块” 最多 7个顶点

            float[] deltaZs = _cacheFloats;
            for (int i = 0; i < inVertNum; i++)
            {
                deltaZs[i] = splitZ - inPoly[i].Z;
            }

            int rowVertexNum = 0;
            int remainPolyVertexNum = 0;

            for (int index = 0, prevIndex = inVertNum - 1; index < inVertNum; index++)
            {
                Vector3 curVertex = inPoly[index];
                // 检查一条边的两个顶点是否在 分割线的两侧, 以更小为左侧，以更大为右侧
                bool leftOfSplit1 = deltaZs[index] >= 0f;
                bool leftOfSplit2 = deltaZs[prevIndex] >= 0f;

                float curDeltaZ = deltaZs[index];
                float prevDeltaZ = deltaZs[prevIndex];
                if (leftOfSplit1 != leftOfSplit2)
                {
                    float splitWeight = prevDeltaZ / (prevDeltaZ - curDeltaZ);
                    Vector3 newSplitVertex = Vector3.Lerp(inPoly[prevIndex], curVertex, splitWeight);
                    outRowPoly[rowVertexNum++] = newSplitVertex;
                    remainPoly[remainPolyVertexNum++] = newSplitVertex;

                    if (curDeltaZ > 0)
                    {
                        outRowPoly[rowVertexNum++] = curVertex;
                    }
                    else if (curDeltaZ < 0)
                    {
                        remainPoly[remainPolyVertexNum++] = curVertex;
                    }
                    // else curDeltaZ 不需要处理。因为此时它等于newSplitVertex, 已经在上面添加了
                }
                else
                {
                    // 这儿略微重构了 recast 的写法
                    // 它的代码有点令人困惑
                    if (curDeltaZ > 0)
                    {
                        outRowPoly[rowVertexNum++] = curVertex;
                    }
                    else if (curDeltaZ == 0)
                    {
                        outRowPoly[rowVertexNum++] = curVertex;
                        remainPoly[remainPolyVertexNum++] = curVertex;
                    }
                    else
                    {
                        remainPoly[remainPolyVertexNum++] = curVertex;
                    }
                }
            }

            outRowVertNum = rowVertexNum;
            outRemainPolyVertNum = remainPolyVertexNum;
        }

        /// <summary>
        /// recast 实现的三角形体素化。
        ///
        /// 该算法先沿着 z 轴将三角形分割，接着再沿着x轴分割
        /// </summary>
        ///
        /// 
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="area"></param>
        /// <param name="minSpanMergeThreshold"></param>
        public unsafe void RasterizeTriangle_Recast(ref Vector3 a, ref Vector3 b, ref Vector3 c, int area, int minSpanMergeThreshold)
        {
            MathUtil.CalcBounds(ref a, ref b, ref c, out Vector3 min, out Vector3 max);
            if (!MathUtil.OverlapBounds(ref min, ref max, ref _minBound, ref _maxBound))
            {
                return;
            }

            float inverseXCellSize = 1f / XCellSize;
            float inverseZCellSize = 1f / ZCellSize;
            float inverseYCellSize = 1f / YCellSize;

            int zStartCellIndex = MathUtil.Clamp((int)((min.Z - _minBound.Z) * inverseZCellSize), 0, ZWidth - 1);
            int zEndCellIndex = MathUtil.Clamp((int)((max.Z - _minBound.Z) * inverseZCellSize), 0, ZWidth - 1);


            // 将输入的多边形以 divZ 为界，分割为两个多边形
            // 最原始的 inPoly 是三角形，因此沿着 z 分割最多产生3 + 2 = 5个顶点。
            // 再接着沿x轴分割时，最多产生 5 + 2 = 7个顶点。也是为什么 _cacheBuff* 长度为 7
            // 由于rasterzation 沿着 z,x坐标各分割一次.
            //

            // 指向将要被分割的多边形（最多4个顶点）
            Vector3[] zInPoly = _cacheBuf1;
            // 指向切出的 “条”,最多5个顶点
            Vector3[] zOutRow = _cacheBuf2;

            // 指向 分割后 剩余的多边形（最多4个顶点）
            Vector3[] zRemainPoly = _cacheBuf3;

            zInPoly[0] = a;
            zInPoly[1] = b;
            zInPoly[2] = c;

            int zInPolyVertNum = 3;

            for (int iz = zStartCellIndex; iz <= zEndCellIndex; iz++)
            {
                float zStart = _minBound.Z + iz * ZCellSize;
                float zEnd = zStart + ZCellSize;

                // inPoly 被分割成 “条“ outRow 以及 剩余多边形 remainPoly
                DividePolyByZAlix(zInPoly, zInPolyVertNum, zOutRow, out int zOutRowVertNum, zRemainPoly, out int zRemainPolyVertNum, zEnd);

                // 分割之后，我们 zInPoly不再需要。出于优化目的，我们用它来保存下一轮的 zRemainPoly.
                // 技巧:通过 swap 交换两个 poly 正好达到目的.
                ValueUtil.Swap(ref zInPoly, ref zRemainPoly);
                zInPolyVertNum = zRemainPolyVertNum;

                // 如果切出的“条”小于三个顶点，说明这是一边边界的竖线，忽略它
                if (zOutRowVertNum < 3) continue;

                // 现在我们沿着x轴切割 刚从z轴切割下的“条”
                Vector3[] xInPoly = zOutRow;
                int xInPolyVertNum = zOutRowVertNum;

                // 计算x最小最大值
                float minX;
                float maxX;
                minX = maxX = zOutRow[0].X;
                for (int i = 1; i < zOutRowVertNum; i++)
                {
                    float vx = zOutRow[i].X;
                    if (vx < minX)
                    {
                        minX = vx;
                    }
                    else if (vx > maxX)
                    {
                        maxX = vx;
                    }
                }

                // 计算起始 x cell index
                int xStartCellIndex = MathUtil.Clamp((int)((minX - _minBound.X) * inverseXCellSize), 0, XWidth - 1);
                int xEndCellIndex = MathUtil.Clamp((int)((maxX - _minBound.X) * inverseXCellSize), 0, XWidth - 1);


                Vector3[] xOutRow = _cacheBuf4;

                // freeInPoly 在z轴循环中已经不使用，我们不再分配 _cache
                Vector3[] xRemainPoly = _cacheBuf5;

                for (int ix = xStartCellIndex; ix <= xEndCellIndex; ix++)
                {
                    float xStart = _minBound.X + ix * XCellSize;
                    float xEnd = xStart + XCellSize;
                    DividePolyByXAlix(xInPoly, xInPolyVertNum, xOutRow, out int xOutVertNum, xRemainPoly, out int xRemainPolyVertNum, xEnd);

                    ValueUtil.Swap(ref xInPoly, ref xRemainPoly);
                    xInPolyVertNum = xRemainPolyVertNum;

                    if (xOutVertNum < 3) continue;

                    // 计算 最大最小y值
                    float minY;
                    float maxY;
                    minY = maxY = xOutRow[0].Y;
                    for (int k = 1; k < xOutVertNum; k++)
                    {
                        float vy = xOutRow[k].Y;
                        if (vy < minY)
                        {
                            minY = vy;
                        }
                        else if (vy > maxY)
                        {
                            maxY = vy;
                        }
                    }

                    if (maxY > _maxBound.Y)
                    {
                        maxY = _maxBound.Y;
                    }
                    if (minY < _minBound.Y)
                    {
                        minY = _minBound.Y;
                    }

                    if (minY > maxY)
                    {
                        continue;
                    }

                    // 最终计算得 (x,y)处 体素柱的 上下界
                    int yMinCellIndex = (int)((minY - _minBound.Y) * inverseYCellSize);
                    int yMaxCellIndex = (int)((maxY - _minBound.Y) * inverseYCellSize);
                    AddSpan(ix, iz, yMinCellIndex, yMaxCellIndex, area, minSpanMergeThreshold);
                }
            }

        }



        /// <summary>
        /// 三角形体素化。
        /// 与标准的三角形体素的不同，我们不需要体素化垂直方向（也即沿y轴）的体素，只需要计算
        /// 某xz坐标下y值范围，故算法也不会相同。
        /// 体素化有一定的误差容忍，因此我们可以容忍为了性能而造成的部分不精确生成（比如没有完全覆盖三角形）
        /// 
        /// 参见文章
        /// http://jeffdwoskin.com/compgraphics/voxel/vox.pdf
        /// https://www.gamasutra.com/blogs/DavidRosen/20091202/86030/Triangle_Mesh_Voxelization.php
        /// https://scialert.net/fulltext/?doi=itj.2007.1286.1289
        /// </summary>
        ///
        /// 
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="area"></param>
        /// <param name="minSpanMergeThreshold"></param>
        public void RasterizeTriangle_Custom(ref Vector3 a, ref Vector3 b, ref Vector3 c, int area, int minSpanMergeThreshold)
        {
            MathUtil.CalcBounds(ref a, ref b, ref c, out Vector3 min, out Vector3 max);
            if (!MathUtil.OverlapBounds(ref min, ref max, ref _minBound, ref _maxBound))
            {
                return;
            }

            // TODO 对于 平面x以及z方向斜率都 <=1 优化.
            // 计算会比较简单


            // TODO 优化 按 x 与 z 轴中 长度排序，取其中较小的轴，以减少迭代次数
            // 按 x 轴 排序
            ref Vector3 p1 = ref a;
            ref Vector3 p2 = ref b;
            ref Vector3 p3 = ref c;

            #region sort by x
            if (a.X < b.X)
            {
                if (b.X < c.X)
                {
                    // a < b < c
                }
                else if (a.X < c.X)
                {
                    // a < c < b
                    p2 = ref c;
                    p3 = ref b;
                }
                else
                {
                    //c < a < b
                    p1 = ref c;
                    p2 = ref a;
                    p3 = ref b;
                }
            }
            else
            {
                if (b.X > c.X)
                {
                    // c < b < a
                    p1 = ref c;
                    p3 = ref a;
                }
                else if (a.X < c.X)
                {
                    // b < a < c
                    p1 = ref b;
                    p2 = ref a;
                }
                else
                {
                    // b < c < a
                    p1 = ref b;
                    p2 = ref c;
                    p3 = ref a;
                }
            }
            #endregion

            float inverseXCellSize = 1f / XCellSize;
            float inverseZCellSize = 1f / ZCellSize;
            float inverseYCellSize = 1f / YCellSize;

            int ix1 = MathUtil.Clamp((int)((p1.X - _minBound.X) * inverseXCellSize), 0, XWidth);
            int ix2 = MathUtil.Clamp((int)((p2.X - _minBound.X) * inverseXCellSize), 0, XWidth);
            int ix3 = MathUtil.Clamp((int)((p3.X - _minBound.X) * inverseXCellSize), 0, XWidth);


            int iy1 = Math.Max((int)((p1.Y - _minBound.Y) * inverseYCellSize), 0);
            int iy2 = Math.Max((int)((p2.Y - _minBound.Y) * inverseYCellSize), 0);
            int iy3 = Math.Max((int)((p3.Y - _minBound.Y) * inverseYCellSize), 0);

            int iz1 = MathUtil.Clamp((int)((p1.Z - _minBound.Z) * inverseZCellSize), 0, ZWidth);
            int iz2 = MathUtil.Clamp((int)((p2.Z - _minBound.Z) * inverseZCellSize), 0, ZWidth);
            int iz3 = MathUtil.Clamp((int)((p3.Z - _minBound.Z) * inverseZCellSize), 0, ZWidth);


            if (ix1 < ix2)
            {
                // p1p2, p1p3 构成两条夹边
                // 斜率较大的 倾向于随着x递增取下方格子
                // 斜率较小的 倾向于取上方格子
                //int deltaY1 = iy2 > iy1 ? 1 : -1;
                //int deltaZ1 = iz2 > iz1 ? 1 : -1;
                //int deltaY2 = iy3 > iy1 ? 1 : -1;
                //int deltaZ2 = iy3 > iy1 ? 1 : -1;
                // [x1, x2)
                for (int ix = ix1; ix < ix2; ix++)
                {
                    int jy1 = iy1 + (2 * (ix - ix1) + 1) * (iy2 - iy1) / (ix2 - ix1) / 2;
                    int jz1 = iz1 + (2 * (ix - ix1) + 1) * (iz2 - iz1) / (ix2 - ix1) / 2;

                    int jy2 = jy1 + (2 * (ix - ix1) + 1) * (iy3 - iy1) / (ix2 - ix1) / 2;
                    int jz2 = jz1 + (2 * (ix - ix1) + 1) * (iz3 - iz1) / (ix2 - ix1) / 2;

                    RasterzationYZLine(ix, jy1, jz1, jy2, jz2);
                }

            }
            else if (ix2 < ix3)
            {
                // ix1 == ix2 < ix3

            }

            else
            {
                // ix1 == ix2 == ix3
            }


            {
                //int deltaY1 = iy3 > iy2 ? 1 : -1;
                //int deltaZ1 = iz3 > iz2 ? 1 : -1;
                //int deltaY2 = iy3 > iy1 ? 1 : -1;
                //int deltaZ2 = iy3 > iy1 ? 1 : -1;
                // [x2, x3)
                for (int ix = ix2; ix < ix3; ix++)
                {
                    int jy1 = iy2 + (ix - ix2 + 1) * (iy2 - iy1) / (ix3 - ix2);
                    int jz1 = iz1 + (ix - ix2 + 1) * (iz2 - iz1) / (ix3 - ix2);

                    int jy2 = jy1 + (ix - ix2 + 1) * (iy3 - iy1) / (ix2 - ix1);
                    int jz2 = jz1 + (ix - ix2 + 1) * (iz3 - iz1) / (ix2 - ix1);

                    RasterzationYZLine(ix, jy1, jz1, jy2, jz2);
                }

                if (ix2 < ix3)
                {
                    // ix3
                    int jy1 = iy3 + (iy2 - iy3) / (ix2 - ix3) / 2;
                    int jz1 = iz3 + (iz2 - iz3) / (ix2 - ix3) / 2;

                    int jy2 = iy3;
                    int jz2 = iz3;

                    RasterzationYZLine(ix3, jy1, jz1, jy2, jz2);
                }
                else if (ix1 < ix2)
                {
                    // ix1 < ix2 == ix3
                    int jy1 = iy2;
                    int jz1 = iz2;

                    int jy2 = iy3;
                    int jz2 = iz3;

                    RasterzationYZLine(ix3, jy1, jz1, jy2, jz2);
                }
                else
                {
                    // ix1 == ix2 == ix3
                    RasterzationYZPlane(ix3, iy1, iz1, iy2, iz2, iy3, iz3);
                }
            }
        }



        private void RasterzationYZLine(int ix, int jy1, int jz1, int jy2, int jz2)
        {
            if (jy1 < jy2)
            {
                for (int j = jy1; j <= jy2; j++)
                {

                }
            }
            else
            {

            }

        }


        private void RasterzationYZPlane(int ix, int jy1, int jz1, int jy2, int jz2, int jy3, int jz3)
        {

        }


        private void AddSpan(int x, int z, int minY, int maxY, int area, int minSpanMergeThreshold)
        {
            ref VoxelPillar vexelPillar = ref VoxelPillarXZArray[x, z];
            if (vexelPillar.Spans == null)
            {
                vexelPillar.Spans = new List<VoxelHeightSpan>();
            }
            vexelPillar.Spans.Add(new VoxelHeightSpan(minY, maxY, area));
        }
    }
}
