using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class QuadMesh : UdonSharpBehaviour
{
    [Tooltip("Width/Height/Depth in model space")] public Vector3 meshDimensions = Vector3.one;
    [SerializeField, Tooltip("Uncheck for circular/sphere point distribution")] 
    public bool isRectangular = false;
    [Tooltip("#  across")] 
    public Vector3Int pointsAcross = new Vector3Int(16,16,16);
    [Tooltip("Make even particle count by ensuring an even number of X points")] 
    public bool makeEvenCount = false;
    [SerializeField, Tooltip("Create triangles, off for billboards as Quads")]
    bool useTriangles = true;
    [SerializeField,Tooltip("Material Template")]
    public Material material;
    
    Mesh mesh;
    MeshFilter mf;
    MeshRenderer mr;

    // Only Uncomment serialization for verification in editor 
    //[SerializeField]
    float radiusSq;
    //[SerializeField]
    Vector3 arraySpacing;
    [SerializeField]
    Vector3 arrayRadius;
    //[SerializeField]
    Vector3 arrayOrigin;
    // Only Uncomment for verification in editor 
    //[SerializeField]
    private Vector3[] vertices;
    //[SerializeField]
    private Vector2[] uvs;
    //[SerializeField]
    int[] triangles;
    [SerializeField]
    int numDecals;
    //[SerializeField]
    int numVertices;
    int numTriangles;
    private bool generateMesh()
    {
        if (mf == null)
            return false;

        arrayRadius = meshDimensions * 0.5f;
        pointsAcross.x = Mathf.Min(pointsAcross.x, 1);
        pointsAcross.y = Mathf.Min(pointsAcross.y, 1);
        pointsAcross.z = Mathf.Min(pointsAcross.z, 1);

        Vector3Int numGridPoints = pointsAcross;

        numGridPoints.x = numGridPoints.x % 2 == 1 ? numGridPoints.x : numGridPoints.x + 1;
        numGridPoints.y = numGridPoints.y % 2 == 1 ? numGridPoints.y : numGridPoints.y + 1;
        numGridPoints.z = numGridPoints.z % 2 == 1 ? numGridPoints.z : numGridPoints.z + 1;

        arraySpacing = new Vector3((numGridPoints.x <= 1) ? meshDimensions.x : (meshDimensions.x/(numGridPoints.x-1)),
                                (numGridPoints.y <= 1) ? meshDimensions.y : (meshDimensions.y / (numGridPoints.y - 1)),
                                (numGridPoints.z <= 1) ? meshDimensions.z : (meshDimensions.z/(numGridPoints.z -1)));
        int halfGridX = numGridPoints.x / 2;
        //Debug.Log($"Grid Points: {numGridPoints}\n" +
        //          $"Half Grid X: {halfGridX}\n" +
        //          $"Array Spacing: {arraySpacing}\n" +
        //          $"Array Radius: {arrayRadius}");
        radiusSq = arrayRadius.x + (arraySpacing.x * 0.1f);
        radiusSq *=radiusSq;
        arrayOrigin = Vector3.zero;
        if (isRectangular)
            arrayOrigin = Vector3.Scale(-arrayRadius, new Vector3(numGridPoints.x <= 1 ? 0 : 1, numGridPoints.y <= 1 ? 0 : 1, numGridPoints.z <= 1 ? 0 : 1));
        
        Vector3 decalPos = arrayOrigin;

        numVertices = 0;
        numTriangles = 0;
        float decalWidth = Mathf.Min(arraySpacing.x, arraySpacing.y);

        Vector3 vxOffset0 = (Vector3.down + Vector3.right) * 0.5f;
        Vector3 vxOffset1 = (Vector3.down + Vector3.left) * 0.5f;
        Vector3 vxOffset2 = (Vector3.up   + Vector3.right) * 0.5f;
        Vector3 vxOffset3 = (Vector3.up   + Vector3.left) * 0.5f; // Only for quad
        if (useTriangles)
        {
            vxOffset0 = new Vector2(0.5f, -0.288675135f);
            vxOffset1 = new Vector2(-0.5f, -0.288675135f);
            vxOffset2 = new Vector2(0, 0.57735027f);
        }

        vxOffset0 *= decalWidth;
        vxOffset1 *= decalWidth;
        vxOffset2 *= decalWidth;
        vxOffset3 *= decalWidth;

        Vector2 uv0 = useTriangles ? new Vector2(-0.366025403f, 0) : Vector2.zero;
        Vector2 uv1 = useTriangles ? new Vector2(1.366025403f, 0) : Vector2.right;
        Vector2 uv2 = useTriangles ? new Vector2(0.5f, 1.5f) : Vector2.up;
        Vector2 uv3 = Vector2.one;
        numDecals = (isRectangular && makeEvenCount) ? (halfGridX*2 * numGridPoints.y * numGridPoints.z) : numGridPoints.x * numGridPoints.y * numGridPoints.z;
        triangles = new int[numDecals * (useTriangles ? 3 : 6)];
        numVertices = numDecals * (useTriangles ? 3 : 4);
        vertices = new Vector3[numVertices];
        uvs = new Vector2[numVertices];
        //Debug.Log($"Calculated {numDecals} decals, allocating {vertices.Length} vertices and {triangles.Length} triangle indices.");
        bool positionInRange = true;
        for (int nPlane = 0; nPlane < numGridPoints.z; nPlane++)
        {
            decalPos.y = arrayOrigin.y;
            for (int nRow = 0; nRow < numGridPoints.y; nRow++)
            {
                decalPos.x = arrayOrigin.x;
                for (int nCol = 0; nCol < numGridPoints.x; nCol++)
                {
                    if (isRectangular)
                    {
                        if (makeEvenCount)
                        {
                            positionInRange = (nCol != halfGridX);
                        }
                        else
                        {
                            positionInRange = true;
                        }
                    }
                    else
                    {
                        float rsq = Vector3.Dot(decalPos, decalPos);
                        positionInRange = rsq <= radiusSq;
                    }
                    if (positionInRange)
                    {

                        triangles[numTriangles++] = numVertices;
                        triangles[numTriangles++] = numVertices + 2;
                        triangles[numTriangles++] = numVertices + 1;
                        if (!useTriangles)
                        {
                            triangles[numTriangles++] = numVertices + 2;
                            triangles[numTriangles++] = numVertices + 3;
                            triangles[numTriangles++] = numVertices  + 1;
                        }
                        uvs[numVertices] = uv0;
                        vertices[numVertices++] = decalPos + vxOffset0;

                        uvs[numVertices] = uv1;
                        vertices[numVertices++] = decalPos + vxOffset1;
                        uvs[numVertices] = uv2;
                        vertices[numVertices++] = decalPos + vxOffset2;

                        if (!useTriangles)
                        {
                            uvs[numVertices] = uv3;
                            vertices[numVertices++] = decalPos + vxOffset3;
                        }
                    }
                    decalPos.x += arraySpacing.x;
                }
                decalPos.y += arraySpacing.y;
            }
            decalPos.z += arraySpacing.z;
        }
        //Debug.Log($"Generated { numVertices} vertices for {numTriangles/3} triangles.");
        mesh = mf.mesh;
        mesh.Clear();
        if (triangles.Length >= 32767)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        triangles = null;
        vertices = null;
        uvs = null;
        if (material != null)
        {
            material.SetVector("_ArraySpacing", arraySpacing);
            if (material.HasProperty("_ArrayDimension"))
            {
                Vector4 pointVec = new Vector4(numGridPoints.x, numGridPoints.y, numGridPoints.z, numGridPoints.x * numGridPoints.y * numGridPoints.z);
                material.SetVector("_ArrayDimension", pointVec);
            }
            mr.material = material;
        }
        return true;
    }
    void OnEnable()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        generateMesh();
    }
}
