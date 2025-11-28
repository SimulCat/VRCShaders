Shader"SimulCat/CRT/Prob Density Screen CRT"
{
    Properties
    {
        _Color("Colour", color) = (1, 1, 1, 1)
        _ColorNeg("Colour Base", color) = (0, 0.3, 1, 0)
        _Visibility("Display Visibility", Range(0,100)) = 10
        _SlitCount("Slit Count",Integer) = 2
        _RowCount("Row Count",Integer) = 2
        _SlitPitch("Slit Pitch",float) = 448
        _RowPitch("Row Pitch",float) = 448
        _SlitWidth("Slit Width", float) = 12.0
        _SlitHeight("Slit Height", float) = 12.0
        _SlitsToScreen("Distance Slits to Screen", float) = 4
        _ScreenWidth("Screen Width", float) = 1.0
        _ScreenHeight("Screen Height", float) = 1.0
        _ParticleP("Particle Momentum", float) = 1.0
        _MinParticleP("Min Particle Momentum", float) = 1.0
        _MaxParticleP("Max Particle Momentum", float) = 1.0
        _MomentumMap("Momentum Map H", 2D ) = "black" {}
        _MapMaxP("Max momentum H", float ) = 1.0
        _MapMaxI("Max integral H", float ) = 1.0
        _MomentumMapY("Momentum Map V", 2D ) = "black" {}
        _MapMaxPy("Max momentum V", float ) = 1.0
        _MapMaxIy("Max integral V", float ) = 1.0
    }


CGINCLUDE

    #include "UnityCustomRenderTexture.cginc"

    #define MX(U) tex2D(_MomentumMap, float2(U))
    #define MY(U) tex2D(_MomentumMapY, float2(U))
    
        float4 _Color;
        float4 _ColorNeg;
        float  _Visibility;

        int _SlitCount;
        int _RowCount;
        float _SlitPitch;
        float _RowPitch;
        float _SlitWidth;
        float _SlitHeight;
        float _SlitsToScreen;
        float _ScreenWidth;
        float _ScreenHeight;
        float _ParticleP;
        float _MinParticleP;
        float _MaxParticleP;
        sampler2D _MomentumMap;
        float _MapMaxP;
        float _MapMaxI;
        sampler2D _MomentumMapY;
        float _MapMaxPy;
        float _MapMaxIy;


// Hashing
    /*
    Description:
	    pcg hash function for when all you need is basic integer randomization, not time/spatially structured noise as in snoise.
	    from article by Nathan Reed
	    https://www.reedbeta.com/blog/hash-functions-for-gpu-rendering/
        and source paper
        https://jcgt.org/published/0009/03/02/
    */
    uint pcg_hash(uint input)
    {
        uint state = input * 747796405u + 2891336453u;
        uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
        return (word >> 22u) ^ word;
    }

    // Hash float from zero to max
    float RandomRange(float rangeMax, uint next)
    {
        float div;
        uint hsh;
        hsh = pcg_hash(next) & 0x7FFFFF;
        div = 0x7FFFFF;
        return rangeMax * ((float) hsh / div);
    }



// Sample the distribution of cumulative probability
float4 sampleDistributionX(float momentum)
{
    float mometumFrac = clamp((momentum/_MapMaxP)*0.5,-0.5,0.5);
    return MX(float2(mometumFrac+.5,0.5));
}

float4 sampleDistributionY(float momentum)
{
    float mometumFrac = clamp((momentum/_MapMaxPy)*0.5,-0.5,0.5);
    return MY(float2(mometumFrac+.5,0.5));
}

float sampleEdgesX (float distance, float xDelta, float slitHalf)
{
    float theta = atan2(xDelta - slitHalf,distance);
    float momentum = sin(theta) * _ParticleP;
    float4 probTop = sampleDistributionX(momentum);

    theta = atan2(xDelta + slitHalf,distance);
    momentum = sin(theta) * _ParticleP;
    float4 probLwr = sampleDistributionX(momentum);

    return (abs(probTop.y - probLwr.y))*(_Visibility/_MapMaxI);
}


float sampleEdgesY (float distance, float yDelta, float rowHalf)
{
    float theta = atan2(yDelta - rowHalf,distance);
    float momentum = sin(theta) * _ParticleP;
    float4 probTop = sampleDistributionY(momentum);

    theta = atan2(yDelta + rowHalf,distance);
    momentum = sin(theta) * _ParticleP;
    float4 probLwr = sampleDistributionY(momentum);

    return (abs(probTop.y - probLwr.y))*(_Visibility/_MapMaxIy);
}


/*
            if (numSlots > 0)
            {
                verticalDelta /= _graphicsRowPitch;
                verticalDelta -= (int)verticalDelta;
                if (!slotsOdd)
                {
                    if ((verticalDelta < minRowFrac) || (verticalDelta > maxRowFrac))
                        return true;
                }
                else
                {
                    if ((verticalDelta > minRowFrac) && (verticalDelta < maxRowFrac))
                        return true;
                }
            }
            if (numSlits > 0)
            {
                horizDelta /= _graphicsSlitPitch;
                horizDelta -= (int)horizDelta;
                if (!slitsOdd)
                {
                    if ((horizDelta < minColFrac) || (horizDelta > maxColFrac))
                        return true;
                }
                else
                {
                    if ((horizDelta > minColFrac) && (horizDelta < maxColFrac))
                        return true;
                }
            }

*/
int postionInsideAperture(int numHoles, float holePitch, float holeWidth, float offset)
{
    // Distance to centre of leftmost slit
    float leftHoleCentre = -(max((numHoles - 1),0) * holePitch) *0.5;
    float leftEdge = leftHoleCentre - (holeWidth * 0.5);
    float normOffset = frac((offset-leftEdge)/holePitch)* holePitch;
    return (int)((offset >= leftEdge) && (offset <= -leftEdge) && (normOffset <= holeWidth));
}


float4 fragShadow(v2f_customrendertexture i) : SV_Target
{
    float2 pixPos = i.globalTexcoord.xy;
    pixPos.x *= _ScreenWidth;
    pixPos.y *= _ScreenHeight;
    pixPos.x -= _ScreenWidth/2.0;
    pixPos.y -= _ScreenHeight/2.0;
    int result = postionInsideAperture(_SlitCount,_SlitPitch,_SlitWidth,pixPos.x);
    result *= postionInsideAperture(_RowCount,_RowPitch,_SlitHeight,pixPos.y);

    //float3 col = _Color.rgb*result;
    float3 col = (float3)(result,result,result);
    return float4(col,result*_Visibility);
}

float4 frag(v2f_customrendertexture i) : SV_Target
{
    float resultX = 0;
    float resultY = 0;
    float2 pos = i.globalTexcoord.xy;
    pos.x *= _ScreenWidth;
    pos.y *= _ScreenHeight;
    pos.x -= _ScreenWidth/2.0;
    pos.y -= _ScreenHeight/2.0;
     
    _SlitCount = max(_SlitCount,1);
    _RowCount = max(_RowCount,1);
    
    float slitPosX = (_SlitCount - 1) * _SlitPitch *0.5;
    float slitPosY = (_RowCount - 1) * _RowPitch *0.5;

    float halfSlitWidth = _SlitWidth * 0.5;
    float halfSlitHeight = _SlitHeight * 0.5;

    for (int nAperture = 0; nAperture < _SlitCount; nAperture++)
    {
        resultX += sampleEdgesX(_SlitsToScreen, (pos.x - slitPosX), halfSlitWidth)/_SlitCount;
        slitPosX -= _SlitPitch;
    }


    for (int nAperture = 0; nAperture < _RowCount; nAperture++)
    {
        resultY += sampleEdgesY(_SlitsToScreen, (pos.y - slitPosY), halfSlitHeight)/_RowCount;
        slitPosY -= _RowPitch;
    }

    resultX *= resultY;
    float3 col = (float3)(resultX,resultX,resultX);//  lerp(_ColorNeg,_Color,resultX).rgb;

    return float4(col,(resultX+0.05)*_Visibility);
}



ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Off
        Pass
        {
            Name "Duane"
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Name "Shadow"
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment fragShadow
            ENDCG
        }
        Pass
        {
            Name "Huygens"
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            ENDCG
        }
    }
}