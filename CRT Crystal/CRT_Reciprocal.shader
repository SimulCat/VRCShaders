Shader"SimulCat/CRT/Reciprocal Lattice"
{
    /*

   */
    Properties
    {
        _CellDimensions("Cell Dims", vector) = (1,1,1,1)
        _CellType("Cubic 0, FCC 1, BCC 2",float) = 0
        _BeamVector("Incident Beam",vector) = (0,0,0,0)
    }

CGINCLUDE

#include "UnityCustomRenderTexture.cginc"
    

    float4 _CellDimensions;
    float _CellType;
    float4 _BeamVector;

// Each point in the reciprocal lattice has a 
float4 frag(v2f_customrendertexture i) : SV_Target
{
    float4 reciprocal = float4(0,0,0,-1);
    float3 myPos = i.globalTexcoord;
    return reciprocal;
}

ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Off
        Pass
        {
            Name "Update"
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            ENDCG
        }
    }
}