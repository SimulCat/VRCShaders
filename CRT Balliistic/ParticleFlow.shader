Shader "SimulCat/Ballistic/Particle Scattering 2D"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Color("Particle Colour", color) = (1, 1, 1, 1)
        _ColourMap("Colour lookUp", 2D) = "black" {}
        _Visibility("Visibility",Range(0.0,1.0)) = 1.0
        _MomentumMap("Momentum Map", 2D ) = "black" {}
        _MapMaxP("Map max momentum", float ) = 1

        _SlitCount("Num Sources",float) = 2
        _SlitPitch("Slit Pitch",float) = 0.3
        _SlitWidth("Slit Width", float) = 0.05
        _BeamWidth("Beam Width", float) = 1
        _GratingOffset("Grating X Offset", float) = 0

        _ParticleP("Particle Momentum", float) = 1
        _MinParticleP("Min Momentum", float) = 1
        _MaxParticleP("Max Momentum", float) = 1
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
        Tags 
        { 
            "Queue"="Transparent" "IgnoreProjector"="True"  
            "RenderType"="Transparent" "PreviewType"="Plane" 
        }
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

            #include "UnityCG.cginc"
		    //#include "../include/spectrum_zucconi.cginc"
		    #include "../include/pcg_hash.cginc"

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
            
            //#define M(U) tex2D(_MomentumMap, float2(U))
            #define M(U) tex2Dlod(_MomentumMap, float4(U))

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Visibility;

            sampler2D _MomentumMap;
            float4 _MomentumMap_ST;
          
            sampler2D _ColourMap;
            float4 _ColourMap_ST;

            float _SlitCount;
            float _SlitPitch;
            float _SlitWidth;
            float _BeamWidth;
            float _GratingOffset;
            float _ParticleP;
            float _MinParticleP;
            float _MaxParticleP;

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
                UNITY_SETUP_INSTANCE_ID(v);
    			UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                int slitCount = max(round(_SlitCount),1);
                float slitPitchScaled = _SlitPitch/_Scale;
                float slitWidthScaled = _SlitWidth/_Scale;
                float gratingWidthScaled = (slitCount-1)*slitPitchScaled + slitWidthScaled;
                float beamWidth = max (_BeamWidth/_Scale,(gratingWidthScaled + slitWidthScaled));

                // Get hash of quad ID and also random 0-1;
                uint idHash = pcg_hash(v.id/3);
                float hsh01 = (float)(idHash & 0x7FFFFF);
                float div = 0x7FFFFF;
                hsh01 = (hsh01/div);
                float hshPlusMinus = (hsh01*2.0)-1.0;
                
                // Shift slit centre to a randomly chosen slit number 
                int nSlit = (idHash >> 8) % slitCount;
                // Set slitCenter to left-most position
                float leftSlitCenter = -(slitCount - 1)*slitPitchScaled*0.5;
                float slitCenter = leftSlitCenter + (nSlit * slitPitchScaled);
                float leftEdge = leftSlitCenter - slitWidthScaled*0.5;
                // check if gratingmakes sense;

                // Now to pick a start position within theslit
                float startHash = RandomRange(1.0,idHash ^ 0xAC3FFF)-0.5;
                float speedHash = RandomRange(2.0,idHash >> 3)-1.0;
                float startPosY =  (_GratingOffset > 0.00001) ? (beamWidth * startHash) : slitCenter + (startHash * slitWidthScaled);
                float normPos = frac((startPosY-leftEdge)/slitPitchScaled)*slitPitchScaled;
                // check if particle y pos is valid;
                bool validPosY = (startPosY >= leftEdge) && (startPosY <= (-leftEdge)) && (normPos <= slitWidthScaled);
                //(0.0 > (frac((rightEdge - startPosY)/slitPitchScaled)*slitPitchScaled + slitWidthScaled));


                float3 centerOffset;

                switch(v.id%3)
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
                float3 triCentreInMesh = v.vertex - vertexOffset;

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

                // Divide time by period to get fraction of the cycle.
                float cycles = ((_Play * _Time.y + (1-_Play)*_PauseTime)-_BaseTime)/cyclePeriod;
                float cycleTime = frac(cycles + continuous*hsh01)*cyclePeriod - pulseMax;
                float timeOffset =  pulseDuration * invPi * asin(hshPlusMinus);
                float trackDistance = (cycleTime + timeOffset)*vScale*voffset;
                float gratingDistance = _GratingOffset/_Scale;
                float postGratingDist = max(0.0,trackDistance-gratingDistance);
                float preGratingDist = min(gratingDistance,trackDistance);

                float2 startPos = float2(preGratingDist-(localGridCentre.x),startPosY);
                float momentumHash = RandomRange(2, idHash);
                float particleP = _ParticleP*voffset;
                float momentumFrac = (particleP - _MinParticleP)/(_MaxParticleP-_MinParticleP);
                float3 sample = sampleMomentum(particleP,momentumHash-1.0);
                float2 particlePosXY = startPos + sample.xy*postGratingDist;
                validPosY = validPosY || (trackDistance <= gratingDistance);
                int  posIsInside = (int)(validPosY)*floor(sample.z)*int((abs(particlePosXY.x) < localGridCentre.x) && (abs(particlePosXY.y) <= localGridCentre.y));
               
                // Check inside bounding box
                particlePosXY = posIsInside*particlePosXY + (1-posIsInside)*triCentreInMesh.xy;

                float3 triCentreInModel = float3(particlePosXY,0.0);


                triCentreInModel = posIsInside * triCentreInModel + (1-posIsInside)*triCentreInMesh; 
                vertexOffset *= markerScale;                    // Scale the quad corner offset to world, now we billboard
                v.vertex.xyz =  triCentreInModel+vertexOffset;
                // billboard the triangle
                float4 camModelCentre = float4(triCentreInModel,1.0);
                float4 camVertexOffset = float4(vertexOffset,0);
                // Three steps in one line
                //      1) Inner step is to use UNITY_MATRIX_MV to get the camera-oriented coordinate of the centre of the billboard.
                //         Here, the xy coords of the billboarded vertex are always aligned to the camera XY so...
                //      2) Just add the scaled xy model offset to lock the vertex orientation to the camera view.
                //      3) Transform the result by the Projection matrix (UNITY_MATRIX_P) and we now have the billboarded vertex in clip space.
                o.vertex = mul(UNITY_MATRIX_P,mul(UNITY_MATRIX_MV, camModelCentre) + camVertexOffset);
                
                //Non-billboard standard code
                //o.vertex = UnityObjectToClipPos (v.vertex);
                float4 colSample = tex2Dlod(_ColourMap,float4(momentumFrac,0.5,0,0));

                o.color = float4(colSample.rgb,-.5 + posIsInside * 1.5);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
		        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 col = tex2D(_MainTex, i.uv);
                col.rgb *= i.color.rgb;
                col.a *= i.color.a;
                if(col.a < 0)
                {
					clip(-1);
					col = 0;
				}
                col *= _Visibility;
                return col;
            }
            ENDCG
        }
    }
}
