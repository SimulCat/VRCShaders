Shader "SimulCat/Crystal/3D Lattice"
{

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LatticePitch("Lattice Pitch", Vector) = (0.1,0.1,0.1,0.1)
        _ArraySpacing("Array Spacing", Vector) = (1.0,1.0,1.0,1.0)
        _DecalScale ("Marker Size", Range(0.03,10)) = 2.5
        _BeamVector ("Local Beam Vector", Vector) = (1,0,0,0)
        _MaxMinP("Max / Min Momentum", Vector) = (1,0,0,0)
        _LatticeType("Cell Type 0=Simple, 1=Ionic, 2=Face Centred, 3=Body Centred", Integer) = 0
        [MaterialToggle]_HighlightEwald("Highlight Ewald Sphere",Integer) = 1
        [MaterialToggle]_OriginAtCorner("Origin at corner",Integer) = 1
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


        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
				uint id : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
    			UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _LatticePitch; // Crystal/Reciprocal Lattice Spacing
            float4 _ArraySpacing; // Initial Quad Mesh Spacing

            float4 _BeamVector;
            float _DecalScale;
            float4 _MaxMinP;
            int _LatticeType;
            int _HighlightEwald;
            int _OriginAtCorner;
            float _Scale;
            
            // Cubic Cell reciprocal lattice points start at corner (all three even)
           int isLatticePoint(int3 idx, float latticeType)
            {
                int offsetToCorner = (_OriginAtCorner) ? -1 : 0;
                idx.x += offsetToCorner;
                idx.y += offsetToCorner;
                idx.z += offsetToCorner;
                int sum = (abs(idx.x) & 1) + (abs(idx.y) & 1) + (abs(idx.z) & 1);

                int cubic = (latticeType < 1 || latticeType > 3);
                int ionic = (latticeType == 1);
                int face = (latticeType == 2);
                int body = (latticeType == 3);
                int zero = (sum == 0);
                int one = (sum == 1);
                int three = (sum == 3);
                return ionic + cubic + face*(three + one) + body*(three + zero);
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
    			UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                uint quadID = v.id/3;
                uint cornerID = v.id%3;
                float3 centerOffset;
                /*
                      vxOffset0 = new Vector2(0.5f, -0.288675135f);
                      vxOffset1 = new Vector2(-0.5f, -0.288675135f);
                        vxOffset2 = new Vector2(0, 0.57735027f);
                */
                switch(cornerID)
                {
                    case 2:
                        centerOffset = float3(0,0.57735027,0); 
                        break;
                    case 1:
                        centerOffset = float3(-.5,-0.288675135,0);
                        break;
                    default:
                        centerOffset = float3(0.5,-0.288675135,0);
                        break;
                }

                float3 vertexOffset = centerOffset*_ArraySpacing;
/* Quad
                float3 halfSpacing = (_ArraySpacing.xyz)*0.5;
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
                */
                float3 decalCenterInMesh = v.vertex - vertexOffset;
                
                int3 indices = int3(round(decalCenterInMesh/_ArraySpacing));
                // Now Scale to lattice
                float3 quadCenterInLattice = indices * _LatticePitch;


                float reactionImpulse = length(quadCenterInLattice);
                float validP = -1. + 2.*(int)(reactionImpulse <= _MaxMinP.x*2.05); // 1.05 to add a smidgen more to radius

                // Now project the X-Ray beam with respect to the lattice point
                float3 normReflect = normalize(reactionImpulse > 0.00001 ? quadCenterInLattice : _BeamVector);
                // Check relationship to beam vector (Cosine of lattice vector vs beam)
                float cosineBeamDirection = -dot(normReflect, _BeamVector.xyz);
                float projectedBeamMaxP = _MaxMinP.x*cosineBeamDirection;
                float reactionHalf = reactionImpulse *0.5;
                int inBeam = projectedBeamMaxP >= reactionHalf;
                float specFrac = inBeam ? 1.0 - reactionHalf/projectedBeamMaxP : 0.0; 
                float markerScale =  (inBeam*(0.5*_HighlightEwald + 0.5) + .5)*_DecalScale;
                float4 col = inBeam ? float4(spectral_frac(specFrac), 1.) : float4(0.3,0.3,0.3,0.3);
                v.vertex=float4((quadCenterInLattice+vertexOffset),0.);
                quadCenterInLattice *= _Scale;

                vertexOffset *= markerScale; // Scale the quad corner offset to world, now we billboard
                float4 camModelCentre = float4(quadCenterInLattice,1.0);
                float4 camVertexOffset = float4(vertexOffset,0.0);
                // Three steps in one line
                //      1) Inner step is to use UNITY_MATRIX_MV to get the camera-oriented coordinate of the centre of the billboard.
                //         Here, the xy coords of the billboarded vertex are always aligned to the camera XY so...
                //      2) Just add the scaled xy model offset to lock the vertex orientation to the camera view.
                //      3) Transform the result by the Projection matrix (UNITY_MATRIX_P) and we now have the billboarded vertex in clip space.

                o.vertex = mul(UNITY_MATRIX_P,mul(UNITY_MATRIX_MV, camModelCentre) + camVertexOffset);

                float a = -0.001 + isLatticePoint(indices,_LatticeType);
                a *= validP;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = float4(col.xyz,1)*a;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // sample the texture
                float4 col = tex2D(_MainTex, i.uv);
                col.rgb *= i.color.rgb;
                col.a *= i.color.a;
                if(col.a < 0)
                {
					clip(-1);
					col = 0;
				}
                return col;
            }
            ENDCG
        }
    }
}
