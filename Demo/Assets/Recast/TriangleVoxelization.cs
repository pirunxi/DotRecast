using System.Collections;
using System.Collections.Generic;
using System;
using Perfect.DotRecast;
using UnityEngine;
using System.Linq;

[Serializable]
public struct Triangle
{
    public Vector3 p1;
    public Vector3 p2;

    public Vector3 p3;
}

public class TriangleVoxelization : MonoBehaviour
{

    public List<Triangle> triangles = new List<Triangle>();

    public float xCellSize = 1;
    public float yCellSize = 1;
    public float zCellSize = 1;



    private List<Triangle> _oldTriangles = new List<Triangle>();

    private float _oldXCellSize;
    private float _oldYCellSize;

    private float _oldZCellSize;

    private Vector3 _minBound;
    private Vector3 _maxBound;

    private VoxelField _field;

    // Start is called before the first frame update
    public void Start()
    {

    }

    private void CalcBounds(Vector3 a, Vector3 b, Vector3 c, out Vector3 min, out Vector3 max)
    {
        float minx = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
        float maxx = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        float miny = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
        float maxy = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
        float minz = Mathf.Min(a.z, Mathf.Min(b.z, c.z));
        float maxz = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
        min = new Vector3(minx, miny, minz);
        max = new Vector3(maxx, maxy, maxz);
    }

    private bool CheckDataChanges()
    {
        if (_field == null)
        {
            return true;
        }


        if (xCellSize != _oldXCellSize || yCellSize != _oldYCellSize || zCellSize != _oldZCellSize)
        {
            return true;
        }

        if (_oldTriangles == null && triangles == null)
        {
            return false;
        }

        if (_oldTriangles == null)
        {
            _oldTriangles = new List<Triangle>();
        }

        if (triangles == null)
        {
            triangles = new List<Triangle>();
        }

        if (triangles.Count != _oldTriangles.Count)
        {
            return true;
        }
        for (int i = 0; i < triangles.Count; i++)
        {
            var t1 = triangles[i];
            var t2 = _oldTriangles[i];
            if (t1.p1 != t2.p1 || t1.p2 != t2.p2 || t1.p3 != t2.p3)
            {
                return true;
            }
        }
        return false;


    }


    void RebuildData()
    {
        if (xCellSize < 1e-2)
        {
            xCellSize = 0.1f;
        }
        _oldXCellSize = xCellSize;

        if (yCellSize < 1e-2)
        {
            yCellSize = 0.1f;
        }
        _oldYCellSize = yCellSize;

        if (zCellSize < 1e-2)
        {
            zCellSize = 0.1f;
        }
        _oldZCellSize = zCellSize;

        _oldTriangles = new List<Triangle>(triangles);

        float minx = 0, maxx = 0, miny = 0, maxy = 0, minz = 0, maxz = 0;

        foreach (var t in triangles)
        {
            var a = t.p1;
            var b = t.p2;
            var c = t.p3;

            minx = Mathf.Min(minx, Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
            maxx = Mathf.Max(maxx, Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            miny = Mathf.Min(miny, Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
            maxy = Mathf.Max(maxy, Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            minz = Mathf.Min(minz, Mathf.Min(a.z, Mathf.Min(b.z, c.z)));
            maxz = Mathf.Max(maxz, Mathf.Max(a.z, Mathf.Max(b.z, c.z)));
        }

        minx = Mathf.Max(minx, -100f);
        miny = Mathf.Max(miny, -100f);
        minz = Mathf.Max(minz, -100f);
        maxx = Mathf.Min(maxx, 100f);
        maxy = Mathf.Min(maxy, 100f);
        maxz = Mathf.Min(maxz, 100f);

        _minBound = new Vector3(minx, miny, minz);
        _maxBound = new Vector3(maxx, maxy, maxz);
    }

    // Update is called once per frame
    public void Update()
    {

        if (CheckDataChanges())
        {
            RebuildData();

            int xWidth = Mathf.Max(40, (int)((_maxBound.x - _minBound.x) / xCellSize));
            int zWidth = Mathf.Max(40, (int)((_maxBound.z - _minBound.z) / zCellSize));
            _field = new VoxelField(xWidth, zWidth,
            new System.Numerics.Vector3(_minBound.x, _minBound.y, _minBound.z),
             new System.Numerics.Vector3(_maxBound.x, _maxBound.y, _maxBound.z),
             xCellSize, yCellSize, zCellSize);

            int area = 1;
            int threshold = 3;

            foreach (var t in triangles)
            {
                var a = t.p1;
                var b = t.p2;
                var c = t.p3;
                var va = new System.Numerics.Vector3(a.x, a.y, a.z);
                var vb = new System.Numerics.Vector3(b.x, b.y, b.z);
                var vc = new System.Numerics.Vector3(c.x, c.y, c.z);
                _field.RasterizeTriangle_Recast(ref va, ref vb, ref vc, area, threshold);
            }
            UnityEngine.Debug.LogFormat("==={0}", triangles.Count);
        }


    }

    public void OnDrawGizmos()
    {
        // Draw a yellow sphere at the transform's position
        if (_field == null)
        {
            return;
        }

        Gizmos.color = Color.blue;
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        mesh.Clear();
        var pos = transform.position;
        var t1 = triangles[0];
        mesh.vertices = new Vector3[] { t1.p1 - pos, t1.p2 - pos, t1.p3 - pos };
        mesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0) };
        mesh.triangles = new int[] { 0, 2, 1 };
        mesh.normals = new Vector3[] { new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1) };
        //Gizmos.DrawMesh(mesh);

        Gizmos.color = Color.red;

        for (int x = 0; x < _field.XWidth; x++)
        {
            for (int z = 0; z < _field.ZWidth; z++)
            {
                VoxelPillar vp = _field.VoxelPillarXZArray[x, z];
                if (vp.Spans != null)
                {
                    foreach (VoxelHeightSpan span in vp.Spans)
                    {
                        for (int y = span.MinY; y <= span.MaxY; y++)
                        {
                            float px = _field.MinBound.X + _field.XCellSize * (x + 0.5f);
                            float py = _field.MinBound.Y + _field.YCellSize * (y + 0.5f);
                            float pz = _field.MinBound.Z + _field.ZCellSize * (z + 0.5f);
                            Gizmos.DrawWireCube(new Vector3(px, py, pz), new Vector3(xCellSize * 1f, yCellSize * 1f, zCellSize * 1f));
                        }
                    }
                }
            }
        }
    }

}
