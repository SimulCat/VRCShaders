Shader "SimulCat/Crystal/3D Lattice Model"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color("Pos Ion Colour",Color) = (0,.7,1.,1.)
        _ColorN("Neg Ion Colour ",Color) = (1,.7,0,1.)
        _LatticePitch("Lattice Point Pitch", Vector) = (0.1,0.1,0.1,0.1)
        _ArraySpacing("Array Spacing", Vector) = (1.0,1.0,1.0,1.0)
        
        _DecalScale ("Marker Size", Range(0.03,10)) = 2.5
        _LatticeType("Unit Cell: 0=Simple, 1=Ionic, 2=Face Centred, 3=Body Centred", Integer) = 0
        [MaterialToggle]_OriginAtCorner("Cell origin at corner", Integer) = 0
        [MaterialToggle]_LimitToBase("Limit to Base Cell", Integer) = 1
        _Scale("Scale Lattice",Float) = 0.25
    }

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
            float4 _Color;
            float4 _ColorN;

            float4 _LatticePitch; // Crystal/Reciprocal Lattice Spacing
            float4 _ArraySpacing; // Initial Quad Mesh Spacing
            float _DecalScale;

            int _LatticeType;
            int _OriginAtCorner; // Display format of cell Lattice or Reciprocal 0=normal != 0 = reciprocal
            int _LimitToBase; // Limit the index of lattice points to display
            float _Scale;
            
            // Cubic Cell reciprocal lattice points start at corner (all three even)
           int2 checkLatticePoint(int3 idx, int latticeType)
            {
                int cubic = (latticeType < 1 || latticeType > 3);
                int ionic = (latticeType == 1);
                int face = (latticeType == 2);
                int body = (latticeType == 3);
                int maxCell = 3;
                maxCell -= (_OriginAtCorner && ((cubic+ionic) > 0)) ? 2 : 0;
                int withinLimit = (int)((_LimitToBase == 0) || 
                    ((abs(idx.x) + abs(idx.y) + abs(idx.z)) <= maxCell));


                int3 offsetToCorner = (_OriginAtCorner) ? int3(-1,-1,-1) : int3(0,0,0);
                idx += offsetToCorner;

                int sum = (abs(idx.x) & 1) + (abs(idx.y) & 1) + (abs(idx.z) & 1);
                int zero = (sum == 0);
                int one = (sum == 1);
                int two = (sum == 2);
                int three = (sum == 3);
                int isPoint = ionic + cubic + face*(three + one) + body*(zero + three);
                isPoint *= withinLimit;

                int notIon = ((cubic + three + ionic*one) > 0);
                return int2(isPoint, notIon);
            }

           v2f vert (appdata v)
           {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
    		    UNITY_TRANSFER_INSTANCE_ID(v, o);
			    //UNITY_INITIALIZE_OUTPUT(v2f, o);
			    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                uint triangleID = v.id/3;
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

                float3 quadCenterInMesh = v.vertex - vertexOffset;
                
                int3 latticePoint = int3(round(quadCenterInMesh/_ArraySpacing));
                // Now Scale to lattice
                float3 quadCenterInLattice = latticePoint * _LatticePitch;

                float markerScale =  _DecalScale;

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

                int2 pointType=checkLatticePoint(latticePoint,_LatticeType);
                float4 col = _Color * pointType.y + _ColorN * (1-pointType.y);

                float a = -0.0001 + pointType.x;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = float4(col.xyz,1)*a;
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
                return col;
            }
           ENDCG
        }
    }
}
