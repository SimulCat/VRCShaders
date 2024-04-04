using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class QuadMesh : UdonSharpBehaviour
{
    [Tooltip("Width/Height/Depth in model space")] public Vector3 meshDimensions = Vector3.one;
    [SerializeField, Tooltip("Uncheck for circular/sphere point distribution")] public bool fillRectangle = false;
    [SerializeField, Tooltip("Center array around mesh origin")] public bool centerArrayOrigin = true;
    [Tooltip("# quads across")] public Vector3Int pointsAcross = new Vector3Int(16,16,16);
    [Tooltip("Snap points to origin (adds 1 point when origin at model center & even #points across)")] 
    public bool snapOrigin = true;
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
    Vector3 arraySpacing;
    [SerializeField]
    float arrayRadius;
    [SerializeField]
    Vector3 arrayOrigin;

    // Only Uncomment for verification in editor 
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

        Vector3 arrayRadius = centerArrayOrigin ? meshDimensions * 0.5f : meshDimensions;
        
        Vector3Int numGridPoints = pointsAcross;
        if (numGridPoints.x < 1) numGridPoints.x = 1;
        if (numGridPoints.y < 1) numGridPoints.y = 1;
        if (numGridPoints.z < 1) numGridPoints.z = 1;
        if (centerArrayOrigin && snapOrigin)
        {
            numGridPoints.x = numGridPoints.x % 2 == 1 ? numGridPoints.x : numGridPoints.x + 1;
            numGridPoints.y = numGridPoints.y % 2 == 1 ? numGridPoints.y : numGridPoints.y + 1;
            numGridPoints.z = numGridPoints.z % 2 == 1 ? numGridPoints.z : numGridPoints.z + 1;
        }

        arraySpacing = new Vector3((numGridPoints.x <= 1) ? meshDimensions.x : (meshDimensions.x/(numGridPoints.x-1)),
                                (numGridPoints.y <= 1) ? meshDimensions.y : (meshDimensions.y / (numGridPoints.y - 1)),
                                (numGridPoints.z <= 1) ? meshDimensions.z : (meshDimensions.z/(numGridPoints.z -1)));

        radiusSq = arrayRadius.x + (arraySpacing.x * 0.5f);
        radiusSq *=radiusSq;

        numQuads = numGridPoints.x*numGridPoints.y*numGridPoints.z;
        numVertices = numQuads * 4;
        vertices = new Vector3[numVertices];
        uvs = new Vector2[numVertices];
        triangles = new int[numQuads * 6];

        arrayOrigin = Vector3.zero;
        if (centerArrayOrigin)
        {
            arrayOrigin = Vector3.Scale(-arrayRadius, new Vector3(numGridPoints.x <= 1 ? 0 : 1, numGridPoints.y <= 1 ? 0 : 1, numGridPoints.z <= 1 ? 0 : 1));
        }
        Vector3 quadPos = arrayOrigin;

        numVertices = 0;
        numTriangles = 0;
        float quadDim = Mathf.Min(arraySpacing.x, arraySpacing.y) * 0.5f;
        Vector3 right = Vector3.right * quadDim;
        Vector3 up = Vector3.up * quadDim;
        Vector3 upright = up + right;
        Vector3 upleft = up - right;
        Vector3 downright = -up + right;
        Vector3 downleft = -upright;
        bool positionInRange;
        for (int nPlane = 0; nPlane < numGridPoints.z; nPlane++)
        {
            quadPos.y = arrayOrigin.y;
            for (int nRow = 0; nRow < numGridPoints.y; nRow++)
            {
                quadPos.x = arrayOrigin.x;
                for (int nCol = 0; nCol < numGridPoints.x; nCol++)
                {
                    if (fillRectangle)
                    {
                        positionInRange = true;
                    }
                    else
                    {
                        float rsq = Vector3.Dot(quadPos, quadPos);
                        positionInRange = rsq <= radiusSq;
                    }

                    if (positionInRange)
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
                    quadPos.x += arraySpacing.x;
                }
                quadPos.y += arraySpacing.y;
            }
            quadPos.z += arraySpacing.z;
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
            Vector4 pointVec = new Vector4(numGridPoints.x,numGridPoints.y,numGridPoints.z,numGridPoints.x*numGridPoints.y*numGridPoints.z);
            material.SetVector("_QuadSpacing", arraySpacing);
            material.SetVector("_QuadDimension", pointVec);

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
