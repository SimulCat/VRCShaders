Shader "SimulCat/Ballistic/Particle Scattering 3D"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _ColourMap("Colour lookUp", 2D) = "black" {}
        _Visibility("Visibility",Range(0.0,1.0)) = 1.0
        // Horizontal Scattering Momentum Frequency Distribution
        _MomentumMap("Momentum Map Horizontal", 2D ) = "black" {}
        _MapMaxP("Horiz Map max momentum", float ) = 1

        // Vertical Scattering Momentum Frequency Distribution
        _MomentumMapY("Momentum Map Vertical", 2D ) = "black" {}
        _MapMaxPy("Map max Y momentum", float ) = 1

        _SlitWidth("Slit Width", float) = 0.05
        _SlitHeight("Slit Height", float) = 0.2

        _SlitCount("Slit Count",Integer) = 2
        _RowCount("Row Count",Integer) = 1

        _SlitPitch("Slit Pitch",float) = 0.3
        _RowPitch("Row Pitch",float) = 0.3

        _BeamWidth("Beam Width", float) = .5
        _BeamHeight("Beam Height", float) = .2

        _GratingDistance("Grating X Offset", float) = 0
        _WallLimits("Wall Limits", Vector) = (5.0,2.0,1.0)

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
        _Scale("Demo Scale",Range(1.0,10.0)) = 1
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
            
            #define MH(U) tex2Dlod(_MomentumMap, float4(U))
            #define MV(U) tex2Dlod(_MomentumMapY, float4(U))

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Visibility;

            sampler2D _MomentumMap;
            float4 _MomentumMap_ST;

            sampler2D _MomentumMapY;
            float4 _MomentumMapY_ST;
          
            sampler2D _ColourMap;
            float4 _ColourMap_ST;

            float _MapMaxP;
            float _MapMaxPy;

            float _SlitWidth;
            float _SlitHeight;

            int _SlitCount;
            int _RowCount;

            float _SlitPitch;
            float _RowPitch;

            float _BeamWidth;
            float _BeamHeight;

            float _GratingDistance;
            float4 _WallLimits;
            float _ParticleP;
            float _MinParticleP;
            float _MaxParticleP;

            float _MaxVelocity;
            float _PulseWidth;
            float _PulseWidthMax;
            float _SpeedRange;
            float _MapSum;

            float4 _ArraySpacing;
            float4 _ArrayDimension;
            float _MarkerScale;
            float _Scale;

            float _BaseTime;
            float _PauseTime;
            float _Play;

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
                                
                float pH = sampleH.z*sign(rnd01)/incidentP;
                float pV = sampleV.z*sign(rnd02)/incidentP;
              
                float2 pScaledHV = float2(pH,pV);
                float pHVsq = dot(pScaledHV,pScaledHV);
              
                int isValid = pHVsq < 1.0;
                pHVsq = clamp(pHVsq,0.0,0.99999);   
                float pFwd = sqrt(1.0-pHVsq);
                return float4(pFwd,pV,pH,isValid);
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
                float maxDiagonalDistance = length(localGridCentre);
                localGridCentre *= 0.5; // Align centre

                // Scaled values of grating dimensions
                float slitPitch = _SlitPitch/_Scale;
                float slitWidth = _SlitWidth/_Scale;
                float slitHeight = _SlitHeight/_Scale;
                float rowPitch = _RowPitch/_Scale;
                float gratingDistance = _GratingDistance/_Scale;

                _SlitCount = max(1,_SlitCount);
                _RowCount = max(1,_RowCount);
                float gratingWidthScaled = (_SlitCount-1)*slitPitch + slitWidth;
                float beamWidth = max (_BeamWidth,(gratingWidthScaled + slitWidth));
                float gratingHeight = (_RowCount-1)*rowPitch + slitHeight;
                float beamHeight = min(_BeamHeight,(gratingHeight + slitHeight*0.5));

                // Set slitCenter to left-most position
                float leftSlitCentre = -(_SlitCount - 1)*slitPitch*0.5;
                // Set rowCenter to lower-most position
                float lowerSlitCentre = -(_RowCount - 1)*rowPitch*0.5;

                // Get hash of quad ID and also random 0-1;
                uint idHash = pcg_hash(v.id/3);
                float hsh01 = (float)(idHash & 0x7FFFFF);
                float div = 0x7FFFFF;
                hsh01 = (hsh01/div);
                float hshPlusMinus = (hsh01*2.0)-1.0;
                // Also hash for particle speed and start position
                float startHashH = RandomRange(1.0,idHash ^ 0xAC3FFF)-0.5;
                float startHashV = RandomRange(1.0,idHash ^ 0xCA37FF)-0.5;
                float speedHash = RandomRange(2.0,idHash >> 3)-1.0;

                // Shift slit centre to a randomly chosen slit number 
                int nSlit = (idHash >> 8) % _SlitCount;
                // 'Randomly' assign the particle to a row
                int nRow = (idHash >> 10) % _RowCount;

                float slitCenter = leftSlitCentre + (nSlit * slitPitch);
                float leftEdge = leftSlitCentre - slitWidth*0.5;
                // check if gratingmakes sense;

                // Find horizontal position across the slits
                float startPosH =  (_GratingDistance > 0.00001) ? (beamWidth * startHashH) : slitCenter + (startHashH * slitWidth);
                
                float normPosH = frac((startPosH-leftEdge)/slitPitch)*slitPitch;
                // check if particle Horiz pos is valid;
                bool validPosH = (startPosH >= leftEdge) && (startPosH <= (-leftEdge)) && (normPosH <= slitWidth);

                float rowCenter = lowerSlitCentre + (nRow * rowPitch);
                float lowerEdge = lowerSlitCentre - slitHeight*0.5;

                // Find Vertical position
                float startPosV =  (_GratingDistance > 0.00001) ? (beamHeight * startHashV) : rowCenter + (startHashV * slitHeight);
                
                float normPosV = frac((startPosV-lowerEdge)/rowPitch)*rowPitch;
                // check if particle y pos is valid;
                bool validPosV = (startPosV >= lowerEdge) && (startPosV <= (-lowerEdge)) && (normPosV <= slitHeight);

                float particleV = _MaxVelocity*(_ParticleP/_MapMaxP)/_Scale;

                // Now particle scattering and position
                
                int hasPulse = (int)(_PulseWidth > 0);
                int continuous = (int)hasPulse == 0;
                float pulseDuration = hasPulse * _PulseWidth;
                float pulseMax = hasPulse * _PulseWidthMax;
                float voffset = 1 + (_SpeedRange * invPi * asin(speedHash));
                float cyclePeriod = (maxDiagonalDistance/particleV) + pulseMax;

                // Divide time by period to get fraction of the cycle.
                float cycles = ((_Play * _Time.y + (1-_Play)*_PauseTime)-_BaseTime)/cyclePeriod;
                float cycleTime = frac(cycles + continuous*hsh01)*cyclePeriod - pulseMax;
                float timeOffset =  pulseDuration * invPi * asin(hshPlusMinus);
                // Calculate distance travelled
                float trackDistance = (cycleTime + timeOffset)*particleV*voffset;
                bool beforeGrating = (trackDistance <= gratingDistance);
                float postGratingDist = max(0.0,trackDistance-gratingDistance);
                bool stuckGrating = ((!validPosH || !validPosV) && (!beforeGrating) && (postGratingDist < gratingDistance));
 
                postGratingDist = stuckGrating ? 0.0 : postGratingDist;
                float preGratingDist = min(gratingDistance,trackDistance);
                // Calculate the particle position based on the time and velocity
                // Start position is offset from the centre of the emission x,y by the grating distance.
                float3 startPos = float3(preGratingDist-(localGridCentre.x),startPosV,startPosH);

                float momentumHashH = RandomRange(2, idHash & 0xF0F0F0F0);
                float momentumHashV = RandomRange(2, idHash & 0x0F0F0F0F);
                float particleP = _ParticleP*voffset;
                float momentumFrac = (particleP - _MinParticleP)/(_MaxParticleP-_MinParticleP);
                // Sample the horizontal and vertical momentum maps to obtain the momentum Vector
                float4 sample = scatterDirection(particleP,momentumHashH-1.0,momentumHashV-1.0);
                // Limit the post grating distance to avoid extreme angles taking particles out of bounds
                float postGratingFwdMax = (_WallLimits.x > gratingDistance) ? (_WallLimits.x -gratingDistance) : _WallLimits.x;
                postGratingFwdMax = postGratingFwdMax / clamp(sample.x,0.1,1.0);

                float postGratingHorizMax = (_WallLimits.y - sign(sample.z)*startPosH);
                postGratingHorizMax = postGratingHorizMax/max(abs(sample.z),0.001);
                postGratingDist = min(postGratingDist, postGratingFwdMax);
                postGratingDist = min(postGratingDist, postGratingHorizMax);

                float3 particlePos = startPos + sample.xyz*postGratingDist;
                int posIsInside = (int)(beforeGrating || ((validPosH && validPosV) || stuckGrating));
               
                               // Check inside bounding box
                posIsInside *= floor(sample.w)*int((abs(particlePos.x) < localGridCentre.x) && (abs(particlePos.y) <= localGridCentre.y) 
                                && (abs(particlePos.z) <= localGridCentre.z));

                // Update particle position in model to reflect either new position or orgiginal grid position
               float3 triCentreInModel = posIsInside*particlePos + (1-posIsInside)*triCentreInMesh;

                vertexOffset *= _MarkerScale/_Scale;                    // Scale the quad corner offset to world, now we billboard
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
