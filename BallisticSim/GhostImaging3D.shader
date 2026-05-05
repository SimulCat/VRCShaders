Shader "Murpheus/Ballistic/Ghost Imaging 3D"
{

    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _ColourMap("Colour lookUp", 2D) = "black" {}
        _ColourMapMax("Colour Map Max", float) = 1.0
        _ColourMapFloor("Colour Map Floor", float) = 0.25

        _SlitWidth("Slit Width", float) = 0.05

        _SlitCount("Slit Count",Integer) = 2

        _SlitPitch("Slit Pitch",float) = 0.3

        _BeamRadius("Beam Radius", float) = .5

        _InitialP("Initial Momentum", float) = 1.0
        _ParticleVelocity("Particle Velocity", float) = 1.0

        _SourcePos("Source Local Position", Vector) = (-2,0,0,0)
        _BBO_Pos("Down Convert Local Position", Vector) = (-1,0,0,0)
        _BeamSplitPos("Beam Split Local Position", Vector) = (0,0,0,0)
        _BeamSplitNormal("Beam Split Normal", Vector) = (-0.70710678,0,0.70710678,0)
        _GratingPos("Grating Local Position", Vector) = (0,0,0,0)
        _DetectorPos("Detector Local Position", Vector) = (0,0,0,0)
        _ScreenPos("Screen Local Position", Vector) = (0,0,0,0)
        _DwellTime("Dwell Time (secs)", float) =3

        _PulseWidth("Pulse Width",float) = 0
        _PulseWidthMax("Max Pulse Width",float) = 1.5

        // Particle Decal Array
        _ArraySpacing("Array Spacing", Vector) = (0.1,0.1,0.1,0)
        // x,y,z count of array w= total.
        _ArrayDimension("Array Dimension", Vector) = (128,80,1,10240)
        _MarkerScale ("Marker Scale", Range(0.01,10)) = 1
        // Play Control
        _BaseTime("Base Time Offset", Float)= 0
        _PauseTime("Freeze time",Float) = 0
        _Play("Play Animation", Integer) = 1
        _UseQuantumScatter("Use Quantum Scatter", Integer) = 1
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
            
            #define MH(U) tex2Dlod(_MomentumMap, float4(U))
            #define MV(U) tex2Dlod(_MomentumMapY, float4(U))

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _ColourMap;
            float4 _ColourMap_ST;

            float _ColourMapMax;
            float _ColourMapFloor;

        
            float _SlitWidth;

            int _SlitCount;

            float _SlitPitch;

            float _BeamRadius;
            float _InitialP;
            float _ParticleVelocity;


            float4 _SourcePos;
            float4 _BBO_Pos;
            float4 _BeamSplitPos;
            float4 _BeamSplitNormal; // Normal of the plane of the beam splitter, facing towards the source
            float4 _GratingPos;
            float4 _DetectorPos;
            float4 _ScreenPos;
            
            float _DwellTime;

            float _PulseWidth;
            float _PulseWidthMax;

            float4 _ArraySpacing;
            float4 _ArrayDimension;
            float _MarkerScale;

            float _BaseTime;
            float _PauseTime;
            int _Play;
            int _UseQuantumScatter;

            float2 GetRandomPointInCircle(float radius, uint seedT, uint seedR) 
            {
                // 1. Generate random angle [0, 2π]
                float theta = 3.14159265 * RandomRange(2.0,seedT);
    
                // 2. Generate random radius [0, radius] 
                // Uses sqrt for uniform distribution (prevents clustering in center)
                float r = radius * sqrt(RandomRange(1.0,seedR));
    
                // 3. Convert Polar to Cartesian
                float2 randomPoint;
                randomPoint.x = r * cos(theta);
                randomPoint.y = r * sin(theta);
    
                return randomPoint;
            }

            /*
            // Returns the sampled momentum direction as a normalized 3d vector
            float4 scatterDirection(float incidentP,float rnd01, float rnd02)
            {
                float fracMaxH = incidentP/_MapMaxP;
                float4 mapMaxH = MH(float4(fracMaxH,0.5,0,0));
                float lookUpH = mapMaxH.y*abs(rnd01);
                float4 sampleH = MH(float4(lookUpH,0.5,0,0));

                float fracMaxV = incidentP/_MapMaxPy;
                float4 mapMaxV = MV(float4(fracMaxV,0.5,0,0));
                float lookUpV = mapMaxV.y*abs(rnd02);
                float4 sampleV = MV(float4(lookUpV,0.5,0,0));
                                
                float pH = _UseQuantumScatter * sampleH.z*sign(rnd01)/incidentP;
                float pV = _UseQuantumScatter * sampleV.z*sign(rnd02)/incidentP;
              
                float2 pScaledHV = float2(pH,pV);
                float pHVsq = dot(pScaledHV,pScaledHV);
              
                int isValid = pHVsq < 1.0;
                pHVsq = clamp(pHVsq,0.0,0.99999);   
                float pFwd = sqrt(1.0-pHVsq);
                return float4(pFwd,pV,pH,isValid);
            } */

            // 2/Pi
            #define twoDivPi 0.636619772367
            // 4/Pi
            #define invPi 0.318309886183

            #define Pi 3.14159265

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
    			UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Calculate the object space offset of the triangle centre from the vertex position
                float3 centerOffset;
                switch(v.id%3) // Corner is vertex ID % 3
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
                float3 vertexOffset = centerOffset*_ArraySpacing.xyz;
                float3 triCentreInMesh = v.vertex.xyz - vertexOffset;

                float3 localGridCentre = ((_ArrayDimension.xyz - float3(1.0,1.0,1.0)) * _ArraySpacing.xyz);
                localGridCentre *= 0.5; // Align centre

                float gratingWidth = (_SlitCount-1)*_SlitPitch + _SlitWidth;
                float gratingDistance = length(_BeamSplitPos - _GratingPos) + length(_SourcePos - _BeamSplitPos);
                float screenDistance = length(_ScreenPos - _SourcePos);
                // Get hash of quad ID and also random 0-1;
                uint id = v.id/3;
                uint idPair = id & 0xFFFFFFFE;
                int isSecondInPair = (id & 1);
                uint pairHash = pcg_hash(idPair);
                float div = 0x7FFFFF;
                float pairHash0to1 = ((float)(pairHash & 0x7FFFFF))/div;
                float hshPlusMinus = (pairHash0to1*2.0)-1.0;

                // Now get particle XY positions at start and slits based on pairhash;
                float2 initalParticleXY = GetRandomPointInCircle(_BeamRadius, pairHash, pairHash ^ 0xABCDEF);
                float3 initalParticlePos = _SourcePos.xyz + float3(0,initalParticleXY);
                float2 screenParticleXY = GetRandomPointInCircle(gratingWidth*0.75, pairHash ^ 0xFEDCBA, pairHash ^ 0x654321);
                float3 screenParticlePos = _ScreenPos.xyz + float3(0,screenParticleXY);
                float3 particleDirection = normalize(screenParticlePos - initalParticlePos); 
                // Timing
                float cyclePeriod = (screenDistance/_ParticleVelocity) + _DwellTime;

                // Chaeck pulse parameters
                float hasPulse = (int)(_PulseWidth > 0);
                int continuous = (int)hasPulse == 0;

                // Divide time by period to get fraction of the cycle.
                float cycles = ((_Play * _Time.y + (1-_Play)*_PauseTime)-_BaseTime)/cyclePeriod;
                float cycleTime = frac(cycles + continuous*pairHash0to1)*cyclePeriod;

                float pulseDuration = hasPulse * _PulseWidth;

                // deal with pulsing by offsetting the start time of each particle by a fraction of the pulse width based on the pair hash. This way, particles will be distributed across the pulse duration rather than all starting at once.
                float timeOffset =  pulseDuration * invPi * asin(hshPlusMinus);
                float trackDistance = (cycleTime + timeOffset)*_ParticleVelocity;
                float3 particlePos = initalParticlePos + particleDirection*trackDistance;
                float trackToBBO = length(_BBO_Pos - initalParticlePos);
                // Get track distance to splitter;
                float trackToBeamSplit = length(_BeamSplitPos - initalParticlePos);
                float denom = dot(particleDirection, _BeamSplitNormal.xyz);
                if  (denom < 1.0e-6)  trackToBeamSplit = dot((_BeamSplitPos.xyz - initalParticlePos), _BeamSplitNormal.xyz)/denom;

                // Update particle position in model to reflect either new position or original grid position
               float3 triCentreInModel = particlePos;

                vertexOffset *= _MarkerScale;                    // Scale the quad corner offset to world, now we billboard
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
               float p = (0.25 + 0.75*int(trackDistance < trackToBBO))*_InitialP; // Set momentum to _InitalP before BBO and 0.5_InitialP after
               float4 colSample = tex2Dlod(_ColourMap,float4(p,0.5,0,0));
                o.color = float4(colSample.rgb,1.5);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
		        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 col = tex2D(_MainTex, i.uv);
                col.rgb *= i.color.rgb;
                col.a = i.color.a;
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
