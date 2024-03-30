using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class QuadMesh : UdonSharpBehaviour
{
    [SerializeField]
    float ballRadius = 1.0f;
    [SerializeField]
    int radiusPoints = 2;
    [SerializeField]
    bool is2D = true;
    [SerializeField]
    bool rectangular = false;
    [SerializeField]
    public Material material;
    
    Mesh theMesh;
    MeshFilter mf;

    // Serialize for debug
 //   [SerializeField]
    float radiusSq;
    [SerializeField]
    private Vector3Int maxRes = new Vector3Int(3, 3, 3);
    [SerializeField]
    Vector3 gridSteps;
    //[SerializeField]
    Vector3 quadOrigin;
    //[SerializeField]
    private Vector3[] vertices;
    //[SerializeField]
    private Vector2[] uvs;
    //[SerializeField]
    int[] triangles;
    [SerializeField]
    int numQuads;
    [SerializeField]
    int numVertices;
    [SerializeField]
    int numTriangles;
    private bool generateMesh()
    {
        if (mf == null)
            return false;
        if (radiusPoints <= 0 || ballRadius <= 0)
            return false;
        int pointsAcross = (radiusPoints * 2) - 1;
        float gridStep = ballRadius / (radiusPoints-1);

        radiusSq = ballRadius + (gridStep * 0.5f);
        radiusSq *=radiusSq;

        maxRes = is2D ? new Vector3Int(pointsAcross, pointsAcross, 1) : Vector3Int.one * pointsAcross;
        numQuads = pointsAcross * pointsAcross * maxRes.z;
        numVertices = numQuads * 4;
        vertices = new Vector3[numVertices];
        uvs = new Vector2[numVertices];
        triangles = new int[numQuads * 6];

        gridSteps = Vector3.one * gridStep;
        quadOrigin = Vector3.one * -ballRadius;
        if (is2D)
        {
            quadOrigin.z = 0;
            gridSteps.z = 0;
        }
        Vector3 quadPos = quadOrigin;

        numVertices = 0;
        numTriangles = 0;
        Vector3 right = Vector3.right * (gridSteps.x * 0.5f);
        Vector3 up = Vector3.up * (gridSteps.y * 0.5f);
        Vector3 upright = up + right;
        Vector3 upleft = up - right;
        Vector3 downright = -up + right;
        Vector3 downleft = -upright;
        
        for (int nPlane = 0; nPlane < maxRes.z; nPlane++)
        {
            quadPos.y = quadOrigin.y;
            for (int nRow = 0; nRow < maxRes.y; nRow++)
            {
                quadPos.x = quadOrigin.x;
                for (int nCol = 0; nCol < maxRes.x; nCol++)
                {
                    float rsq = Vector3.Dot(quadPos, quadPos);
                    //Debug.Log("QuadMesh r=" + r.ToString());
                    if (rsq <= radiusSq)
                    {

                        triangles[numTriangles++] = numVertices;
                        triangles[numTriangles++] = numVertices + 2;
                        triangles[numTriangles++] = numVertices + 1;
                        triangles[numTriangles++] = numVertices + 2;
                        triangles[numTriangles++] = numVertices + 3;
                        triangles[numTriangles++] = numVertices + 1;

                        uvs[numVertices] = Vector2.zero;
                        vertices[numVertices++] = quadPos + downright;

                        uvs[numVertices] = Vector2.right;
                        vertices[numVertices++] = quadPos + downleft;

                        uvs[numVertices] = Vector2.up;
                        vertices[numVertices++] = quadPos + upright;

                        uvs[numVertices] = Vector2.right + Vector2.up;
                        vertices[numVertices++] = quadPos + upleft;
                    }
                    quadPos.x += gridSteps.x;
                }
                quadPos.y += gridSteps.y;
            }
            quadPos.z += gridSteps.z;
        }
        theMesh = mf.mesh;
        theMesh.Clear();
        numQuads = numVertices / 4;
        int[] meshTris = new int[numTriangles];
        for (int i = 0; i < numTriangles; i++)
            meshTris[i] = triangles[i];
        if (meshTris.Length >= 32767)
            theMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        Vector3[] meshVerts = new Vector3[numVertices];
        Vector2[] meshUVs = new Vector2[numVertices];
        for (int i = 0; i < numVertices; i++)
        {
            meshUVs[i] = uvs[i];
            meshVerts[i] = vertices[i];
        }
        theMesh.vertices = meshVerts;
        theMesh.triangles = meshTris;
        theMesh.uv = meshUVs;
        triangles = null;
        vertices = null;
        uvs = null;
        if (material != null)
        {
            material.SetVector("_MeshSpacing", gridSteps);
            //material.SetFloat("_MarkerSize", value: 0.3f);
        }
        return true;
    }
    void Start()
    {

        mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
            material = mr.material;
        generateMesh();
    }
}
