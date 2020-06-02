using System.Collections;
using System.Collections.Generic;
using Perfect.DotRecast;
using UnityEngine;


public class TriangleVoxelization : MonoBehaviour
{

    public Vector3 a;
    public Vector3 b;
    public Vector3 c;

    public float xCellSize = 1;
    public float yCellSize = 1;
    public float zCellSize = 1;



    private Vector3 _oldA;
    private Vector3 _oldB;

    private Vector3 _oldC;

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
        if (_field == null || a != _oldA || b != _oldB || c != _oldC
        || xCellSize != _oldXCellSize || yCellSize != _oldYCellSize || zCellSize != _oldZCellSize)
        {
            _oldA = a;
            _oldB = b;
            _oldC = c;

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

            CalcBounds(a, b, c, out _minBound, out _maxBound);

            _minBound.x = Mathf.Min(-100f, _minBound.x);
            _minBound.y = Mathf.Min(-100f, _minBound.y);
            _minBound.z = Mathf.Min(-100f, _minBound.z);
            _maxBound.x = Mathf.Max(100f, _maxBound.x);
            _maxBound.y = Mathf.Max(100f, _maxBound.y);
            _maxBound.z = Mathf.Max(100f, _maxBound.z);

            return true;
        }
        return false;
    }

    // Update is called once per frame
    public void Update()
    {
        
        if (CheckDataChanges())
        {
            int xWidth = Mathf.Max(40, (int)((_maxBound.x - _minBound.x) / xCellSize));
            int zWidth = Mathf.Max(40, (int)((_maxBound.z - _minBound.z) / zCellSize));
            _field = new VoxelField(xWidth, zWidth, 
            new System.Numerics.Vector3(_minBound.x, _minBound.y, _minBound.z),
             new System.Numerics.Vector3(_maxBound.x, _maxBound.y, _maxBound.z),
             xCellSize, yCellSize, zCellSize);

            var va = new System.Numerics.Vector3(a.x, a.y, a.z);
            var vb = new System.Numerics.Vector3(b.x, b.y, b.z);
            var vc = new System.Numerics.Vector3(c.x, c.y, c.z);
            int area = 1;
            int threshold = 3;
            _field.RasterizeTriangle_Recast(ref va, ref vb, ref vc, area, threshold);
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
        mesh.vertices = new Vector3[] {a - pos, b - pos, c - pos};
        mesh.uv = new Vector2[] {new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0)};
        mesh.triangles = new int[] {0, 2, 1};
        mesh.normals = new Vector3[] {new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1)};
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
