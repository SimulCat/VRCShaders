// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "SimulCat/Crystal/ReciprocalUnlit"
{

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LatticeSpacing("Lattice Spacing", Vector) = (0.1,0.1,0.1,0.1)
        _QuadSpacing("Quad Array Spacing", Vector) = (1.0,1.0,1.0,1.0)
        _QuadDimension("Quad Array Dimension", Vector) = (1.0,1.0,1.0,1.0)
        _MarkerScale ("Marker Size", Range(0.01,2)) = 0.3
        _BeamVector ("Local Beam Vector", Vector) = (1,0,0,0)
        _MaxMinP("Max / Min Momentum", Vector) = (1,0,0,0)
        _LatticeType("0=Cubic, 1=Ionic, 2=Face Center, 3=Body Center", Float) = 0
        _Scale("Scale Lattice",Float) = 0.25
    }

   CGINCLUDE
		#include "../include/spectrum_zucconi.cginc"
	ENDCG

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend One One
        Cull Off
        Lighting Off
        ZWrite Off
        Fog { Color (0,0,0,0) }



        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            // slightly shorter version for when the scale can be assumed to be uniform all directions.
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
            float4 _LatticeSpacing; // Crystal/Reciprocal Lattice Spacing
            float4 _QuadSpacing; // Initial Quad Mesh Spacing
            float4 _QuadDimension; // Quad Mesh Dimension x,y,z (integer)

            float4 _BeamVector;
            float _MarkerScale;
            float4 _MaxMinP;
            float _LatticeType;
            float _Scale;
            
            // Cubic Cell reciprocal lattice points start at corner (all three even)
           int isReciprocalPoint(int nX, int nY, int nZ, float latticeType)
            {
                int sum = abs(nX) & 1;
                sum += abs(nY) & 1;
                sum += abs(nZ) & 1;
                /*
                if (sum == 0) // on Corner
                    return 1;
                switch (latticeType)
                {
                    case 0:
                    case 1:
                        return 0;
                        break;
                    case 2:
                        return (int)(sum==2);
                        break;
                    case 3:
                        return (int)(sum==3);
                        break;
                }
                return 0;
                */
                int cubic = (int)(latticeType < 2 || latticeType > 3);
                int face = (int)(latticeType == 2);
                int body = (int)(latticeType == 3);
                int zero = (int)(sum == 0);
                int two = (int)(sum == 2);
                int three = (int)(sum == 3);
                return cubic*zero + face*(zero + two) + body*(zero + three);
            }

            v2f vert (appdata v)
            {
                v2f o;
                //UNITY_SETUP_INSTANCE_ID(v);
    			//UNITY_TRANSFER_INSTANCE_ID(v, o);
				//UNITY_INITIALIZE_OUTPUT(v2f, o);
				//UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                uint quadID = v.id/4;
                uint cornerID = v.id%4;
                float3 centerOffset;
                float3 halfSpacing = (_QuadSpacing.xyz)*0.5;
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
                
                int3 indices = int3(round(quadCenterInMesh/_QuadSpacing));
                // Now Scale to lattice
                float3 quadCenterInLattice = indices * _LatticeSpacing;


                float reflectP = length(quadCenterInLattice);
                float validP = -1. + 2.*(int)(reflectP <= _MaxMinP.x*0.5125);

                // Now project the X-Ray beam with respect to the lattice point
                float3 normReflect = normalize(reflectP > 0.00001 ? quadCenterInLattice : _BeamVector);
                // Check relationship to beam vector (Cosine of lattice vector vs beam)
                float cosineBeamDirection = -dot(normReflect, _BeamVector.xyz);
                float projectedMaxP = _MaxMinP.x*cosineBeamDirection;
                float4 col = float4(0.3,0.3,0.3,0.4);
                float markerSize =  _MarkerScale;
                float reactionPx2 = reflectP+reflectP;
                if (projectedMaxP >= reactionPx2)
                {
                    col.xyz = spectral_frac(1.0 - reactionPx2/projectedMaxP)*1.25;
                }                
                else
                {
                    markerSize *= 0.25;
                }
                quadCenterInLattice *= _Scale;

                vertexOffset *= markerSize; // Scale the quad corner offset to world, now we billboard
                float objScale = ObjectScale;
                float4 camModelCentre = float4((quadCenterInLattice * objScale),1.0);
                float4 camVertexOffset = float4(vertexOffset * objScale,1);
                // Three steps in one line
                //      1) Inner step is to use UNITY_MATRIX_MV to get the camera-oriented coordinate of the centre of the billboard.
                //         Here, the xy coords of the billboarded vertex are always aligned to the camera XY so...
                //      2) Just add the scaled xy model offset to lock the vertex orientation to the camera view.
                //      3) Transform the result by the Projection matrix (UNITY_MATRIX_P) and we now have the billboarded vertex in clip space.

                o.vertex = mul(UNITY_MATRIX_P,mul(UNITY_MATRIX_MV, camModelCentre) + camVertexOffset);

                float a = -0.001 + isReciprocalPoint(indices.x,indices.y,indices.z,_LatticeType);
                a *= validP;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = float4(col.xyz,1)*a;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // sample the texture
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
