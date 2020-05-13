using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

namespace Perfect.DotRecast
{
    public struct MarkedTriangle
    {
        public MarkedTriangle(Vector3[] vertices, int indiceX, int indiceY, int indiceZ, int area)
        {
            Vertices = vertices;
            IndiceA = indiceX;
            IndiceB = indiceY;
            IndiceC = indiceZ;
            Area = area;
        }

        private Vector3[] Vertices { get; }

        public int IndiceA { get; }

        public int IndiceB { get; }

        public int IndiceC { get; }

        public int Area { get; set; }



        public ref Vector3 A => ref Vertices[IndiceA];

        public ref Vector3 B => ref Vertices[IndiceB];

        public ref Vector3 C => ref Vertices[IndiceC];

        //public EAreaFlags AreaFlags { get; set; }
    }

    public class MarkedTriangleSet
    {
        public Vector3[] Vertices { get; }

        public List<MarkedTriangle> Triangles { get; }

        public MarkedTriangleSet(Vector3[] verts, List<MarkedTriangle> indices)
        {
            Vertices = verts;
            Triangles = indices;
        }
    }
}
