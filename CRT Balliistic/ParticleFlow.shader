Shader "SimuCat/Ballistic/Particle Dispersion"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Color("Particle Colour", color) = (1, 1, 1, 1)
        _Visibility("Visibility",Range(0.0,1.0)) = 1.0
        _MomentumMap("Momentum Map", 2D ) = "black" {}
        _MapMaxP("Map max momentum", float ) = 1

        _SlitCount("Num Sources",float) = 2
        _SlitPitch("Slit Pitch",float) = 0.3
        _SlitWidth("Slit Width", float) = 0.05
        _BeamWidth("Beam Width", float) = 1
        _GratingOffset("Grating X Offset", float) = 0

        _ParticleP("Particle Momentum", float) = 1
        _MaxVelocity("MaxVelocity", float) = 5
        _SpeedRange("Speed Range fraction",Range(0.0,0.5)) = 0
        _PulseWidth("Pulse Width",float) = 0
        _PulseWidthMax("Max Pulse Width",float) = 1.5
        // Particle Decal Array
        _ArraySpacing("Array Spacing", Vector) = (0.1,0.1,0.1,0)
        // x,y,z count of array w= total.
        _ArrayDimension("Array Dimension", Vector) = (128,80,1,10240)
        _MarkerScale ("Marker Scale", Range(0.01,10)) = 1
        _Scale("Scale Demo",Float) = 1
        _MaxScale("Scale Max",Float) = 5
        // Play Control
        _BaseTime("Base Time Offset", Float)= 0
        _PauseTime("Freeze time",Float) = 0
        _Play("Play Animation", Float) = 1
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
            
            //#define M(U) tex2D(_MomentumMap, float2(U))
            #define M(U) tex2Dlod(_MomentumMap, float4(U))

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Visibility;

            sampler2D _MomentumMap;
            float4 _MomentumMap_ST;
          
            float _SlitCount;
            float _SlitPitch;
            float _SlitWidth;
            float _BeamWidth;
            float _GratingOffset;
            float _ParticleP;

            float _MapMaxP;
            float _MaxVelocity;
            float _PulseWidth;
            float _PulseWidthMax;
            float _SpeedRange;
            float _MapSum;

            float4 _ArraySpacing;
            float4 _ArrayDimension;
            float _MarkerScale;
            float _Scale;
            float _MaxScale;

            float _BaseTime;
            float _PauseTime;
            float _Play;

            float3 sampleMomentum(float incidentP,float rnd01)
            {
                float fracMax = incidentP/_MapMaxP;
                float4 mapMax = M(float4(fracMax,0.5,0,0));
                float lookUp = mapMax.y*abs(rnd01);
                float4 sample = M(float4(lookUp,0.5,0,0));
                float py = sample.z*sign(rnd01);
                int isValid = py < incidentP;
                float sinTheta = clamp(py/incidentP,-1.0,1.0);
                float cosTheta = cos(asin(sinTheta));
                return float3(cosTheta,sinTheta,isValid);
            }

            // 2/Pi
            #define twoDivPi 0.636619772367
            // 4/Pi
            #define invPi 0.318309886183

            v2f vert (appdata v)
            {
                v2f o;
                uint quadID = v.id/3;

                // Get hash of quad ID and also random 0-1;
                uint idHash = pcg_hash(quadID);
                //uint idHashFlip = idHash ^ 0x7FFFF;
                float hsh01 = (float)(idHash & 0x7FFFFF);
                float div = 0x7FFFFF;
                hsh01 = (hsh01/div);
                float hshPlusMinus = (hsh01*2.0)-1.0;
                
                int slitCount = max(round(_SlitCount),1);

                float slitPitchScaled = _SlitPitch/_Scale;
                float slitWidthScaled = _SlitWidth/_Scale;
                float gratingWidthScaled = (slitCount-1)*slitPitchScaled + slitWidthScaled;
                float beamWidth = max (_BeamWidth/_Scale,(gratingWidthScaled + slitWidthScaled));
                
                // Set slitCenter to left-most position
                float leftSlitCenter = -(slitCount - 1)*slitPitchScaled*0.5;

                // Shift slit centre to a randomly chosen slit number 
                int nSlit = (idHash >> 8) % slitCount;
                float slitCenter = leftSlitCenter + (nSlit * slitPitchScaled);
                float leftEdge = leftSlitCenter - slitWidthScaled*0.5;

                // Fresh hash of the particle to pick a start position
                float startHash = RandomRange(1.0,idHash ^ 0xAFAFAF)-0.5;
                float speedHash = RandomRange(2.0,idHash >> 3)-1.0;
                float startPosY =  (_GratingOffset > 0.00001) ? (beamWidth * startHash) : slitCenter + (startHash * slitWidthScaled);
                float normPos = frac((startPosY-leftEdge)/slitPitchScaled)*slitPitchScaled;
                // check if particle y pos is valid;
                bool gratingValid = (startPosY >= leftEdge) && (startPosY <= (-leftEdge)) && (normPos <= slitWidthScaled);
                //(0.0 > (frac((rightEdge - startPosY)/slitPitchScaled)*slitPitchScaled + slitWidthScaled));


                uint cornerID = v.id%3;
                float3 centerOffset;

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

                float markerScale =  _MarkerScale/_Scale;

                float2 localGridCentre = ((_ArrayDimension.xy - float2(1,1)) * _ArraySpacing.xy);
                float maxDiagonalDistance = length(localGridCentre);
                localGridCentre *= 0.5; // Align centre
                float particleVelocity = _MaxVelocity*(_ParticleP/_MapMaxP);

                // Now particle scattering and position
                
                int hasPulse = (int)(_PulseWidth > 0);
                int continuous = (int)hasPulse == 0;
                float pulseDuration = hasPulse * _PulseWidth;
                float pulseMax = hasPulse * _PulseWidthMax;
                float voffset = 1 + (_SpeedRange * invPi * asin(speedHash));
                float vScale = particleVelocity/_Scale;
                float cyclePeriod = (maxDiagonalDistance/vScale) + pulseMax;

                // Divid time by period to get fraction of the cycle.
                float cycles = ((_Play * _Time.y + (1-_Play)*_PauseTime)-_BaseTime)/cyclePeriod;
                float cycleTime = frac(cycles + continuous*hsh01)*cyclePeriod - pulseMax;
                float timeOffset =  pulseDuration * invPi * asin(hshPlusMinus);
                float trackDistance = (cycleTime + timeOffset)*vScale*voffset;
                float gratingDistance = _GratingOffset/_Scale;
                float postGratingDist = max(0.0,trackDistance-gratingDistance);
                float preGratingDist = min(gratingDistance,trackDistance);
                //float markerRadius = _ArraySpacing.x*0.5*markerScale;
                float2 startPos = float2(preGratingDist-(localGridCentre.x),startPosY);
                float momentumHash = RandomRange(2, idHash);
                float3 sample = sampleMomentum(_ParticleP*voffset,momentumHash-1.0);
                float2 particlePos = startPos + sample.xy*postGratingDist;
                gratingValid = gratingValid || (trackDistance <= gratingDistance);
                int  posIsInside = (int)(gratingValid)*floor(sample.z)*int((abs(particlePos.x) < localGridCentre.x) && (abs(particlePos.y) <= localGridCentre.y));
               
                // Check inside bounding box
                particlePos = posIsInside*particlePos + (1-posIsInside)*quadCenterInMesh.xy;

                float3 quadCentreInModel = float3 (particlePos,quadCenterInMesh.z);


                quadCentreInModel = posIsInside * quadCentreInModel + (1-posIsInside)*quadCenterInMesh; 
                vertexOffset *= markerScale;                    // Scale the quad corner offset to world, now we billboard
                v.vertex.xyz=quadCentreInModel+vertexOffset;

                float4 camModelCentre = float4(quadCentreInModel,1.0);
                float4 camVertexOffset = float4(vertexOffset,0);
                // Three steps in one line
                //      1) Inner step is to use UNITY_MATRIX_MV to get the camera-oriented coordinate of the centre of the billboard.
                //         Here, the xy coords of the billboarded vertex are always aligned to the camera XY so...
                //      2) Just add the scaled xy model offset to lock the vertex orientation to the camera view.
                //      3) Transform the result by the Projection matrix (UNITY_MATRIX_P) and we now have the billboarded vertex in clip space.

                o.vertex = mul(UNITY_MATRIX_P,mul(UNITY_MATRIX_MV, camModelCentre) + camVertexOffset);
                /*
                Standard code
                o.vertex = UnityObjectToClipPos (v.vertex);
                */
                o.color = float4(_Color.rgb,-.5 + posIsInside * 1.5);
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
                col *= _Visibility;
                //UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
