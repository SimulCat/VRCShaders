Shader "SimuCat/Crystal/LatticeUnlit"
{

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color("Pos Ion Colour",Color) = (0,.7,1.,1.)
        _ColorN("Neg Ion Colour ",Color) = (1,.7,0,1.)
        _LatticeSpacing("Lattice Spacing", Vector) = (0.1,0.1,0.1,0.1)
        _ArraySpacing("Array Spacing", Vector) = (1.0,1.0,1.0,1.0)

        _DecalScale ("Marker Size", Range(0.03,10)) = 2.5
        _LatticeType("0=Cubic, 1=Ionic, 2=Face Center, 3=Body Center", Float) = 0
        _Scale("Scale Lattice",Float) = 0.25
    }

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
            float4 _Color;
            float4 _ColorN;

            float4 _LatticeSpacing; // Crystal/Reciprocal Lattice Spacing
            float4 _ArraySpacing; // Initial Quad Mesh Spacing
            float _DecalScale;

            float _LatticeType;
            float _Scale;
            
            // Cubic Cell reciprocal lattice points start at corner (all three even)
           int2 checkLatticePoint(int nX, int nY, int nZ, float latticeType)
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
                int cubic = (int)(latticeType < 1 || latticeType > 3);
                int ionic = (int)(latticeType == 1);
                int face = (int)(latticeType == 2);
                int body = (int)(latticeType == 3);
                int zero = (int)(sum == 0);
                int one = (int)(sum == 1);
                int three = (int)(sum == 3);
                return int2(ionic + cubic*three + face*(three + one) + body*(zero + three), three);
            }

            v2f vert (appdata v)
            {
                v2f o;
                //UNITY_SETUP_INSTANCE_ID(v);
    			//UNITY_TRANSFER_INSTANCE_ID(v, o);
				//UNITY_INITIALIZE_OUTPUT(v2f, o);
				//UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

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
/*
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
                float3 quadCenterInMesh = v.vertex - vertexOffset;
                
                int3 indices = int3(round(quadCenterInMesh/_ArraySpacing));
                // Now Scale to lattice
                float3 quadCenterInLattice = indices * _LatticeSpacing;


                float markerScale =  _DecalScale;

                v.vertex=float4((quadCenterInLattice+vertexOffset),0.);
                quadCenterInLattice *= _Scale;

                vertexOffset *= markerScale; // Scale the quad corner offset to world, now we billboard
                float objScale = ObjectScale;
                float4 camModelCentre = float4((quadCenterInLattice * objScale),1.0);
                float4 camVertexOffset = float4(vertexOffset * objScale,0.0);
                // Three steps in one line
                //      1) Inner step is to use UNITY_MATRIX_MV to get the camera-oriented coordinate of the centre of the billboard.
                //         Here, the xy coords of the billboarded vertex are always aligned to the camera XY so...
                //      2) Just add the scaled xy model offset to lock the vertex orientation to the camera view.
                //      3) Transform the result by the Projection matrix (UNITY_MATRIX_P) and we now have the billboarded vertex in clip space.

                o.vertex = mul(UNITY_MATRIX_P,mul(UNITY_MATRIX_MV, camModelCentre) + camVertexOffset);

                int2 pointType = checkLatticePoint(indices.x,indices.y,indices.z,_LatticeType);
                float4 col = _Color * pointType.y + _ColorN * (1-pointType.y);

                float a = -0.0001 + pointType.x;
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
