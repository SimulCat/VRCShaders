Shader"SimulCat/CRT/HuygensImage CRT"
{
    Properties
    {
        _PhaseMap("Phase Map", 2D ) = "black" {}
        _MapWidth("Phase Map Width", float) = 2560.0
        _MapHeight("Phase Map Height",float) = 1440.0
        _ScreenWidth("Screen Width", float) = 2560.0
        _ScreenHeight("Screen Height",float) = 1440.0
        _Wavelength("Wavelength (mm)", float) = 1.0
        _SlitCount("Slit Count",Integer) = 2
        _RowCount("Row Count",Integer) = 2
        _SlitPitch("Slit Pitch",float) = 448
        _RowPitch("Row Pitch",float) = 448
        _SlitWidth("Slit Width", float) = 12.0
        _SlitHeight("Slit Height", float) = 12.0
        _SlitsToScreen("Distance Slits to Screen", float) = 4
    }

    CGINCLUDE
    #define Phase(U) tex2D(_PhaseMap, float2(U))

    #include "UnityCustomRenderTexture.cginc"
        Texture2D _PhaseMap;
        float _MapWidth;
        float _MapHeight;
        float  _ScreenWidth;
        float  _ScreenHeight;
        float  _Wavelength;

        int _SlitCount;
        int _RowCount;
        float _SlitPitch;
        float _RowPitch;
        float _SlitWidth;
        float _SlitHeight;
        float _SlitsToScreen;

    uint pcg_hash(uint input)
    {
        uint state = input * 747796405u + 2891336453u;
        uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
        return (word >> 22u) ^ word;
    }

    // Random from zero to max
    float RandomRange(float rangeMax, uint next)
    {
        float div;
        uint hsh;
        hsh = pcg_hash(next) & 0x7FFFFF;
        div = 0x7FFFFF;
        return rangeMax * ((float)hsh / div);
    }

    float RandomSourcePosition(int numGaps, float gapPitch, float gapWidth, uint rnd)
    {
        float GapOffset;
        float GapInnerPosition;
        float gWidth = numGaps <= 0 ? gapPitch*2 : gapWidth;
        float halfGap = gWidth / 2.0;
        uint nGap;
        GapInnerPosition =  RandomRange(gWidth, rnd++) - halfGap;
        if (numGaps <= 1)
            return GapInnerPosition;
        nGap = pcg_hash(rnd++);
        nGap = nGap % numGaps;
        GapOffset = ((int)nGap - (numGaps - 1.0f) / 2.0f);
        GapOffset *= gapPitch;
        return GapOffset + GapInnerPosition;
    }

    float4 frag(v2f_customrendertexture i) : SV_Target
    {
        float2 pos = i.globalTexcoord.xy;
        pos.x *= _ScreenWidth;
        pos.y *= _ScreenHeight;
        pos.x -= _ScreenWidth/2.0;
        pos.y -= _ScreenHeight/2.0;
        float xDelta, yDelta;
        float result = 0.0;
        int n = 0;
        uint seed = asuint(pos.x) + asuint(pos.y)*73856093u;
        while (n++ < 100)
        {
            xDelta = RandomSourcePosition(_SlitCount, _SlitPitch, _SlitWidth,  seed++);
            yDelta = RandomSourcePosition(_RowCount, _RowPitch, _SlitHeight, seed++);
            result += LerpPixelIntensity(baseX + xDelta, baseY + yDelta);
        }

        return float4(result,result,0,1);
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