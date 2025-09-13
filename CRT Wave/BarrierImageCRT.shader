Shader"SimulCat/Wave/Barrier Image CRT"
{
    Properties
    {
        _Color("Barrier Colour", color) = (1, 1, 1, 1)
        _ColorBackground("Colour Wall", color) = (0, 0, 0, 0)

        _SlitCount("Slit Count",float) = 2
        _SlitWidth("Slit Width", float) = 12.0
        _SlitPitch("Slit Pitch",float) = 448
        _SlitOffset("Slit Offset", float) = 0
        _SlitThickness("Slit Thickness", float) = 0
    }

CGINCLUDE

    #include "UnityCustomRenderTexture.cginc"

    #define M(U) tex2D(_MomentumMap, float2(U))
    
        float4 _Color;
        float4 _ColorBackground;

        float _SlitCount;
        float _SlitPitch;
        float _SlitWidth;
        float _SlitOffset;
        float _SlitThickness;

float4 frag(v2f_customrendertexture i) : SV_Target
{
    float2 pos = i.globalTexcoord.xy;
    // Pixel Positions
    int xPixel = (int)(floor(pos.x * _CustomRenderTextureWidth));
    int yPixel = (int)(floor(pos.y * _CustomRenderTextureHeight));
    int slitCount = max(round(_SlitCount),1);
    // See if pixel within barrier x zone
    float halfThick = _SlitThickness * 0.5;
    int inBarrierZone = (int)((xPixel > (_SlitOffset - halfThick)) && (xPixel < (_SlitOffset + halfThick)));
    
    float halfTankWidth = _CustomRenderTextureHeight * 0.5f;
    float halfGratingWidth = ((_SlitPitch * slitCount-1) + _SlitWidth)*0.5f;
    float leftSlitCenter = -0.5*(slitCount - 1)*_SlitPitch;
    float leftEdge = leftSlitCenter - (_SlitWidth*0.5f);
    float pixelPosY = halfTankWidth-yPixel;
    // find slitCenter to right-most position
    float normPos = frac((pixelPosY-leftEdge)/ _SlitPitch) * _SlitPitch;
    int inValidPosY = (int)((normPos > _SlitWidth) || (pixelPosY < leftEdge) || (pixelPosY > -leftEdge));
    int sourceCount = round(_SlitCount);
    return (float4)(_Color * inBarrierZone * inValidPosY);
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

    }
}