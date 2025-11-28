Shader"SimulCat/CRT/Prob Density Plane CRT"
{
    Properties
    {
        _Color("Colour Wave", color) = (1, 1, 1, 1)
        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _Visibility("Display Contrast",float) = 1.0
        _SlitWidth("Slit Width", Range(1.0,40.0)) = 12.0
        _BeamWidth("Grating Width", float) = 1
        _GratingDistance("Grating Distance", float) = 0
        _ShowBeam("Show Beam", float) = 1
        _ParticleP("Particle Momentum", float) = 1
        _Scale("Simulation Scale",Range(1.0,10.0)) = 1
        _MomentumMap("Momentum Map", 2D ) = "black" {}
        _MapMaxP("Map max momentum", float ) = 1
        _MapMaxI("Map max integral", float ) = 1
    }

CGINCLUDE

    #include "UnityCustomRenderTexture.cginc"

    #define M(U) tex2D(_MomentumMap, float2(U))
    
        float4 _Color;
        float4 _ColorNeg;
        float  _Visibility;

        int _SlitCount;
        float _SlitPitch;
        float _SlitWidth;
        float _BeamWidth;
        float _GratingDistance;
        float _ShowBeam;
        float _ParticleP;
        float _Scale;
        sampler2D _MomentumMap;
        float4 _MomentumMap_TexelSize;
        float _MapMaxP;
        float _MapMaxI;

// Sample the distribution of cumulative probability
float4 sampleDistribution(float momentum)
{
    float mometumFrac = clamp((momentum/_MapMaxP)*0.5,-0.5,0.5);
    return M(float2(mometumFrac+.5,0.5));
}

float sampleEdges (float xDelta, float yDelta, float slitHalf)
{
    int isAfterGrating = int(xDelta >= 0);
    xDelta = abs(xDelta);

    //float thetaTop = atan2(yDelta - slitHalf,xDelta);
    float momentumTop = sin(atan2(yDelta - slitHalf,xDelta)) * _ParticleP;
    float4 probTop = sampleDistribution(momentumTop);

    //float thetaLwr = atan2(yDelta + slitHalf,xDelta);
    float momentumLwr = sin(atan2(yDelta + slitHalf,xDelta)) * _ParticleP;
    float4 probLwr = sampleDistribution(momentumLwr);

    return isAfterGrating*(abs(probTop.y - probLwr.y)/_MapMaxI);
}

float4 frag(v2f_customrendertexture i) : SV_Target
{
    float result = 0;
    float2 pos = i.globalTexcoord.xy;
    int xPixel = (int)(floor(pos.x * _CustomRenderTextureWidth));
    int yPixel = (int)(floor(pos.y * _CustomRenderTextureHeight));
    float yOffsetPx = yPixel - _CustomRenderTextureHeight / 2.0;
    _SlitCount = max(_SlitCount,1);
    float apertureY = (_SlitCount - 1) * _SlitPitch *0.5;
    float halfBeam = max(_BeamWidth,apertureY)*.5;

    float yPos = yOffsetPx * _Scale;
    float xDelta = (xPixel - _GratingDistance) * _Scale;
    float scaleWidth = (_CustomRenderTextureWidth * _Scale);
    float halfAperture = _SlitWidth * 0.5;
    for (int nAperture = 0; nAperture < _SlitCount; nAperture++)
    {
        float yDelta = yPos - apertureY;
        float2 delta = float2(xDelta,yDelta);
        result += sampleEdges (xDelta, yDelta, halfAperture);
        apertureY -= _SlitPitch;
    }
    // Check before aperture
    float before = _ShowBeam * ((_GratingDistance > 0 && xDelta <= 0) ? 2.0 : 0);
    result = result + before*(1.0 - smoothstep(halfBeam,halfBeam*1.3,abs(yPos)));
    float3 col = lerp(_ColorNeg,_Color,result*_Visibility).rgb;

    return float4(col,result*_Visibility+0.2);
}



ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Off
        Pass
        {
            Name "Update"
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            ENDCG
        }

        Pass
        {
            Name "Ballistic"
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            ENDCG
        }
    }
}