Shader "Murpheus/Ballistic/Ghost Imaging 3D"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _ColourMap("Colour lookUp", 2D) = "black" {}

        _MomentumMap("Momentum Map", 2D ) = "black" {}
        _MapMaxP("Map max momentum", float ) = 1

        _SlitCount("Slit Count",Integer) = 2
        _SlitWidth("Slit Width", float) = 0.05
        _SlitPitch("Slit Pitch",float) = 0.3

        _BeamRadius("Beam Radius", float) = .02
        _BBO_ConeAngle("BBO Cone Angle (Radians)", float) = 0.03

        _LaserP("Laser Momentum", float) = 0.8
        _MinParticleP("Min Momentum", float) = 0.0
        _MaxParticleP("Max Momentum", float) = 1.0
        _ParticleSpeed("Particle Speed", float) = 1.0

        _SourcePos("Source Position", Vector) = (-2,0,0,0)
        _BBO_Pos("BBO Down Convert Position", Vector) = (-1,0,0,0)
        _BeamSplitPos("Beam Split Position", Vector) = (0,0,0,0)
        _BeamSplitNormal("Beam Split Normal", Vector) = (-0.70710678,0,0.70710678,0)
        _GratingNormal("Grating Normal", Vector) = (0,0,-1,0)
        _GratingPos("Grating Position", Vector) = (0,0,0,0)
        _DetectorPos("Detector Position", Vector) = (0,0,0,0)
        _DetectorWidth("Detector Width", float) = 0.02
        _BackstopPos("Backstop Position", Vector) = (0,0,7.7,0)
        _ScreenPos("Screen Position", Vector) = (0,0,0,0)
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
        [Toggle] _UseQuantumScatter("Use Quantum Scatter", Integer) = 1
        _CullMode("Cull Setting (0=none, 1=Slits, 2=Detector)", Integer) = 1
        _ShowCoincidence("Show Coincidence (0=none, 1=Both, 2=Just Coincidences)", Integer) = 1
        [Toggle] _LimitHorizontal("Only Horizontal", Integer) = 1
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
            
            #define M(U) tex2Dlod(_MomentumMap, float4(U))

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _ColourMap;
            float4 _ColourMap_ST;

            sampler2D _MomentumMap;
            float4 _MomentumMap_ST;
            float _MapMaxP;
      
            int _SlitCount;
            float _SlitWidth;
            float _SlitPitch;

            float _BeamRadius;
            float _BBO_ConeAngle;
            float _LaserP;
            float _MinParticleP;
            float _MaxParticleP;
            float _ParticleSpeed;


            float4 _SourcePos;
            float4 _BBO_Pos;
            float4 _BeamSplitPos;
            float4 _BeamSplitNormal; // Normal of the plane of the beam splitter, facing towards the source
            float4 _GratingPos;
            float4 _GratingNormal; // Normal of the plane of the grating, facing towards the source
            float4 _DetectorPos;
            float4 _BackstopPos;
            float _DetectorWidth;
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
            int _CullMode;
            int _ShowCoincidence;
            int _LimitHorizontal;

            // 2/Pi
            #define twoDivPi 0.636619772367
            // 4/Pi
            #define INV_PI 0.318309886183

            #define Pi 3.14159265


            float2 GetRandomPointInCircle(float radius, uint seedT, uint seedR) 
            {
                // 1. Generate random angle [0, 2π]
                float theta = 3.14159265 * RandomRange(2.0,seedT);
    
                // 2. Generate random radius [0, radius] 
                // Uses sqrt for uniform distribution (prevents clustering in center)
                //float r = radius * asin(sqrt(RandomRange(1.0,seedR))) * INV_PI;
                float r = radius * sqrt(RandomRange(1.0,seedR));
    
                // 3. Convert Polar to Cartesian
                float2 randomPoint;
                randomPoint.x = r * cos(theta);
                randomPoint.y = r * sin(theta);
    
                return randomPoint;
            }

            float4 scatterParticle(float incidentP, float3 incidentDirection, float signedRand01)
            {
                // Assume paticle incident from Z direction and grating normal is Z, so scatter in XY plane for now. Todo can add support for 3d scattering in future if needed.
                float3 resultDir = incidentDirection;

                float fracMax = incidentP/_MapMaxP;
                float4 mapMax = M(float4(fracMax,0.5,0,0));
                float lookUp = mapMax.y*abs(signedRand01);
                float4 sample = M(float4(lookUp,0.5,0,0));
                resultDir.x += _UseQuantumScatter * sample.z*sign(signedRand01)/incidentP;
                float pHVsq = dot(resultDir.xy, resultDir.xy);
                float isValid = (float)(pHVsq < 1.0);
                pHVsq = clamp(pHVsq,0.0,1.0);   
                resultDir.z = sqrt(1.0-pHVsq);
                return float4(resultDir,isValid);
            }


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

                float gratingDistance = length(_BeamSplitPos - _GratingPos) + length(_SourcePos - _BeamSplitPos);
                float screenDistance = length(_ScreenPos - _SourcePos);
                // Timing
                float cyclePeriod = (screenDistance/_ParticleSpeed) + _DwellTime;

                // Check pulse parameters
                float hasPulse = (int)(_PulseWidth > 0);
                int continuous = (int)hasPulse == 0;

                // Divide time by period to get fraction of the cycle.
                int play = (int)(_Play == 1);
                float cycles = ((play * _Time.y + (1-play)*_PauseTime)-_BaseTime)/cyclePeriod;


                uint nCycle = (uint)cycles;
                // Get hash of quad ID and also random 0-1;
                uint id = v.id/3;
                uint idPair = (id & 0xFFFFFFFE);
                bool isParticleA = ((id & 0x1) == 0);
                uint pairHash = pcg_hash(idPair);
                float div = 0x7FFFFF;
                float pairHash0to1 = ((float)(pairHash & 0x7FFFFF))/div;
                float altHash0to1 = (float)(pcg_hash(0xFEEDBEEF ^ idPair))/div;

                float hshPlusMinus = (pairHash0to1*2.0)-1.0;
                
                // Distance from BBO to Screen, BeamSplitter and Slits
                float distanceBBOtoScreen = length(_ScreenPos - _BBO_Pos);
                float distanceBBOtoSplitter = length(_BeamSplitPos - _BBO_Pos);
                float distanceBBOtoGrating = length(_GratingPos - _BeamSplitPos) + distanceBBOtoSplitter;

                // Now get particle XY positions at start and slits based on pairhash;
                // Find a random slit position for culled particles
                // Shift slit centre to a randomly chosen slit number 
                int nSlit = (idPair >> 8) % _SlitCount;
                float leftSlitCenter = -(_SlitCount - 1)*_SlitPitch*0.5;
                float slitCenter = leftSlitCenter + (nSlit * _SlitPitch);
                float slitLeftEdge = slitCenter - _SlitWidth*0.5;
                float slitWide = _SlitWidth * 1.25; // Add a bit of extra width to make it more likely to get some hits for display purposes, can set to 1.0 for more accuracy if needed.
                float leftWideEdge = slitCenter - slitWide*0.5;
                // Cone radius back from grating to BBO
                float2 vGratingConeXY =  _LimitHorizontal ? float2(RandomRange(2.0,pairHash ^ 0x33CCFFCC)-1.0,0.0) : 
                    GetRandomPointInCircle(1.0,pairHash ^ 0xCC330033,pairHash ^ 0x33CCFFCC);
                 vGratingConeXY = vGratingConeXY * (distanceBBOtoGrating * tan(_BBO_ConeAngle)); 
                 
                 // Pick a random initial particle position within the beam

                float2 initialParticleXY = _LimitHorizontal ? float2(RandomRange(2.0,pairHash ^ 0x5A5AFF00)-1.0,RandomRange(2.0,pairHash ^ 0xA5A500FF)-1.0) : 
                    GetRandomPointInCircle(1, pairHash ^ 0x5A5AFF00 , pairHash ^ 0xA5A500FF);
                 initialParticleXY = initialParticleXY * _BeamRadius;

                float2 vGratingParticleXY = initialParticleXY + vGratingConeXY;
                float gratingWidth = (_SlitCount-1)*_SlitPitch + _SlitWidth * 1.25;
                vGratingParticleXY.x =  _CullMode > 0 ? leftWideEdge + (pairHash0to1 * slitWide) : vGratingParticleXY.x;

                // Assume screen particle starts out along X axis
                
                float3 vGratingParticlePos = float3(distanceBBOtoGrating, vGratingParticleXY.yx);

                // Start particle off in beam travellng in X direction splitting off from BBO with direction to BBO based on random position within beam to slits. 
                float3 posAtSource = _SourcePos.xyz + float3(0,initialParticleXY.yx);
                float3 posAtBBO = float3(_BBO_Pos.x,initialParticleXY.yx);

                float3 initialDirection = float3(1,0,0); // Assuming initial direction is along the X-axis
                float3 particleDirectionB = normalize(vGratingParticlePos - float3(0,initialParticleXY.yx));
                float3 particleDirectionA = reflect(particleDirectionB, float3(0,0,-1));
                float3 reflectedDirection = reflect(particleDirectionB, _BeamSplitNormal.xyz);


                float sourceToBBO = length(posAtBBO - posAtSource);

                float3 screenParticlePos = posAtBBO + particleDirectionA * distanceBBOtoScreen;
                float cycleTime = cyclePeriod * (_Play >= 0 ? frac(cycles + continuous*altHash0to1) : 0.9999);

                float pulseDuration = hasPulse * _PulseWidth;

                // Get track distance to splitter;
                float denom = dot(particleDirectionB, _BeamSplitNormal.xyz);
                float bbOToBeamSplitB = distanceBBOtoSplitter;
                if  (denom < 1.0e-6)  
                     bbOToBeamSplitB = dot((_BeamSplitPos.xyz - posAtBBO), _BeamSplitNormal.xyz)/denom;

                float3 posAtBeamSplitB =  posAtBBO + particleDirectionB*bbOToBeamSplitB;
                

                float splitterToGrating = length(_GratingPos - _BeamSplitPos);
                denom = dot(reflectedDirection, float3(0.0,0.0,-1.0));
                if  (denom < 1.0e-6)  splitterToGrating = dot((_GratingPos.xyz - posAtBeamSplitB), float3(0.0,0.0,-1.0))/denom;
                float3 posAtGrating = posAtBeamSplitB + reflectedDirection*splitterToGrating;
                
                // Now use x component of B leg particle position to check if particle at grating is within slit positions to determine if it is blocked or not.
                float gratingX = posAtGrating.x - _GratingPos.x;
                                // Set slitCenter to left-most position
                bool validPosAtGrating = (gratingX >= slitLeftEdge) && (gratingX <= slitLeftEdge+_SlitWidth);
              
                // Work out particle direction if it passes the grating. 
                float3 postGratingDirection = reflectedDirection;
                float postBBOp = _LaserP*0.5;
                
                postGratingDirection = scatterParticle(postBBOp, reflectedDirection, RandomRange(2.0, pairHash+1)-1.0).xyz;
                float gratingToDetector = length(_DetectorPos - _GratingPos);
                float gratingToBackstop = length(_BackstopPos - _GratingPos);
                bool scatterAligned = length(postGratingDirection.xy*gratingToDetector) <=_DetectorWidth; // Check if scatter is within sensor bounds consider it blocked by grating
                denom = dot(postGratingDirection, float3(0.0,0.0,-1.0));
                if (denom < 1.0e-6) gratingToDetector = dot((_DetectorPos.xyz - posAtGrating), float3(0.0,0.0,-1.0))/denom;
                float3 positionAtDetector = posAtGrating + postGratingDirection*gratingToDetector;
                float3 positionAtBackstop = posAtGrating + postGratingDirection*gratingToBackstop;
                int triggersSensor = (int)(validPosAtGrating && scatterAligned && _ShowCoincidence != 0);// && (abs(positionAtDetector.x - _DetectorPos.x) < _DetectorWidth*0.5));
                bool hitsSensor = (triggersSensor > 0) || (abs(positionAtDetector.x - _DetectorPos.x) < _DetectorWidth*0.5);


                // Finally, work out if particle A has reached the screen and if so, where. If not, use position at detector to work out where it would be based on its current direction.

                // Now calculate the particle pair's distance travelled since emission, to allow us to work out where each particle should be along their respective tracks
                float timeOffset =  pulseDuration * INV_PI * asin(hshPlusMinus);
                float trackDistance = (cycleTime + timeOffset)*_ParticleSpeed;
                float trackPastBBO = max(0.0, trackDistance - sourceToBBO);
                float trackPastBeamSplitB = max(trackPastBBO - bbOToBeamSplitB, 0.0);
                float trackPastGrating = max(trackPastBeamSplitB - splitterToGrating, 0.0);
                bool particleValidB = validPosAtGrating || ((trackPastGrating <= 2.0) || (_Play<=0));
                trackPastGrating = validPosAtGrating ? trackPastGrating : 0.0; 
                float trackPastSensor = hitsSensor || triggersSensor ? 0.0 : trackPastGrating; 


                bool pastBBO = trackPastBBO > 0.0;
                bool pastBeamSplitB = trackPastBeamSplitB > 0.0;
                bool pastScreen = trackDistance >= (length(screenParticlePos - posAtBBO)+sourceToBBO);
                bool pastGrating = trackPastBeamSplitB > splitterToGrating;
                bool pastDetector = trackPastGrating >= gratingToDetector;
                bool pastBackstopPlane = trackPastSensor >= gratingToBackstop;

                float3 preBBOParticlePos = posAtSource + initialDirection*trackDistance;
                float3 particlePosA = pastBBO ? posAtBBO + particleDirectionA*trackPastBBO : preBBOParticlePos;
                particlePosA = pastScreen ? screenParticlePos : particlePosA;

                float3 particlePosB = pastBBO ? posAtBBO + particleDirectionB*trackPastBBO : preBBOParticlePos;
                particlePosB = pastBeamSplitB ? (posAtBeamSplitB + trackPastBeamSplitB*reflectedDirection) : particlePosB;
                particlePosB = pastGrating ? (posAtGrating + trackPastGrating*postGratingDirection) : particlePosB;
                particlePosB = pastDetector && hitsSensor ? positionAtDetector : particlePosB;
                particlePosB = pastBackstopPlane ? positionAtBackstop : particlePosB;

                // Update particle position in model to reflect either new position or original grid position
                float3 triCentreInModel = isParticleA ? particlePosA : particlePosB;

                vertexOffset *= _MarkerScale; // * (0.5 + triggersSensor * 0.5) ;                    // Scale the quad corner offset to world, now we billboard
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
                float pfrac =  (((trackDistance > sourceToBBO) ? postBBOp : _LaserP) - _MinParticleP)/(_MaxParticleP-_MinParticleP);
                float4 colSample = tex2Dlod(_ColourMap,float4(pfrac,0.5,0,0));
                particleValidB = particleValidB && (trackDistance > sourceToBBO);
                int valid = (int)((isParticleA || particleValidB) && (_ShowCoincidence < 2 || triggersSensor > 0)); // Use alpha channel of colour map as validity flag for particle (1=valid, 0=invalid)
                float alpha = valid > 0 ? 1 : -1;
                triggersSensor = triggersSensor * valid;
                if (pastBBO)
                    o.color = float4(triggersSensor*colSample.brr + (1-triggersSensor)*0.25*colSample.rgb, alpha);
                else
                    o.color = float4(colSample.rgb, isParticleA ? 1.5 : -1 );

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
