Shader "SimulCat/Ballistic/Particle Dispersion"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Color("Particle Colour", color) = (1, 1, 1, 1)
        _MomentumMap("Momentum Map", 2D ) = "black" {}
        _MapMaxP("Map max momentum", float ) = 1
        _MapSum("Map sum probability", float ) = 1

        _SlitCount("Num Sources",float) = 2
        _SlitPitch("Slit Pitch",float) = 448
        _SlitWidth("Slit Width", Range(1.0,40.0)) = 12.0
        _ParticleP("Particle p", float) = 1
        // Particle Quad Array
        _QuadSpacing("Quad Array Spacing", Vector) = (0.1,0.1,0.1,0)
        // x,y,z count of array w= total.
        _QuadDimension("Quad Array Dimension", Vector) = (128,80,1,10240)
        _MarkerSize ("Marker Size", Range(0.01,2)) = 1
        _Scale("Scale Demo",Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend One One
        LOD 100
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
		    //#include "../include/spectrum_zucconi.cginc"
		    #include "../include/pcg_hash.cginc"

            #define ObjectScale length(unity_ObjectToWorld._m00_m10_m20)

            #define ObjectScaleVec float3( \
                length(unity_ObjectToWorld._m00_m10_m20),\
                length(unity_ObjectToWorld._m01_m11_m21),\
                length(unity_ObjectToWorld._m02_m12_m22))

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
				uint id : SV_VertexID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            sampler2D _MomentumMap;
            float4 _MomentumMap_TexelSize;
            
            float _SlitCount;
            float _SlitPitch;
            float _SlitWidth;
            float _SamplesPerSlit;
            float _ParticleK;
            float _ParticleP;

            float _MapMaxP;
            float _MapSum;


            float4 _QuadSpacing;
            float4 _QuadDimension;
            float _MarkerSize;
            float _Scale;


            v2f vert (appdata v)
            {
                v2f o;
                uint quadID = v.id/4;
                uint quadHash = pcg_hash(quadID);
                uint cornerID = v.id%4;
                float3 centerOffset;
                float3 halfSpacing = float3(0.5,0.5,0.5)*_QuadSpacing;
                switch(cornerID)
                {
                    case 3:
                        centerOffset = float3(-1,1,0); 
                        break;
                    case 2:
                        centerOffset = float3(1,1,0); 
                        break;
                    case 1:
                        centerOffset = float3(-1,-1,0);
                        break;
                    default:
                        centerOffset = float3(1,-1,0);
                        break;
                }
                float3 vertexOffset = centerOffset*halfSpacing;
                float3 quadCenterInMesh = v.vertex - vertexOffset;
                
                float markerSize =  _MarkerSize;
                float3 quadCenterInDemo = quadCenterInMesh;
                vertexOffset *= markerSize; // Scale the quad corner offset to world, now we billboard
                float objScale = ObjectScale;
                float4 camModelCentre = float4((quadCenterInDemo * objScale),1.0);
                float4 camVertexOffset = float4(vertexOffset * objScale,1);
                // Three steps in one line
                //      1) Inner step is to use UNITY_MATRIX_MV to get the camera-oriented coordinate of the centre of the billboard.
                //         Here, the xy coords of the billboarded vertex are always aligned to the camera XY so...
                //      2) Just add the scaled xy model offset to lock the vertex orientation to the camera view.
                //      3) Transform the result by the Projection matrix (UNITY_MATRIX_P) and we now have the billboarded vertex in clip space.

                o.vertex = mul(UNITY_MATRIX_P,mul(UNITY_MATRIX_MV, camModelCentre) + camVertexOffset);

                o.color = float4(_Color.rgb,1);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                col.rgb *= i.color.rgb;
                col.a *= i.color.a;
                if(col.a < 0)
                {
					clip(-1);
					col = 0;
				}
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
