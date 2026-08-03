Shader"Murpheus/CRT/HuygensImage CRT"
{
    Properties
    {
        _PhaseMap("Phase Map", 2D ) = "black" {}
        _BaseColor("Colour", color) = (1, 1, 1, 1)
        _MapWidth("Phase Map Width", float) = 2560.0
        _MapHeight("Phase Map Height",float) = 1440.0
        _ScreenWidth("Screen Width", float) = 2560.0
        _ScreenHeight("Screen Height",float) = 1440.0
        _SlitCount("Slit Count",Integer) = 2
        _RowCount("Row Count",Integer) = 2
        _SlitPitch("Slit Pitch",float) = 448
        _RowPitch("Row Pitch",float) = 448
        _SlitWidth("Slit Width", float) = 12.0
        _SlitHeight("Slit Height", float) = 12.0
        _SlitsToScreen("Distance Slits to Screen", float) = 4
    }

    CGINCLUDE
    #include "UnityCustomRenderTexture.cginc"

        #define PH(U) tex2D(_PhaseMap, float2(U))

        sampler2D _PhaseMap;
        float4 _BaseColor;
        float _MapWidth;
        float _MapHeight;
        float  _ScreenWidth;
        float  _ScreenHeight;

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
        float2 screenPos = i.globalTexcoord.xy;
        screenPos.x *= _ScreenWidth;
        screenPos.y *= _ScreenHeight;
        screenPos.x -= _ScreenWidth/2.0;
        screenPos.y -= _ScreenHeight/2.0;
        float xDelta, yDelta;
        float2 summedPhase = float2(0.0,0.0);
        int n = 0;
        uint seed = asuint(screenPos.x) + asuint(screenPos.y)*73856093u;
        float2 mapUV;
        float4 Sample;
        while (n++ < 2000)
        {
            xDelta = RandomSourcePosition(_SlitCount, _SlitPitch, _SlitWidth,  seed++);
            yDelta = RandomSourcePosition(_RowCount, _RowPitch, _SlitHeight, seed++);
            mapUV.x = (screenPos.x + xDelta)/_MapWidth + 0.5;
            mapUV.y = (screenPos.y + yDelta)/_MapHeight + 0.5;
            Sample = PH(mapUV);
            summedPhase += float2(cos(Sample.x),sin(Sample.x))*Sample.y;
        }
        float value = dot(summedPhase, summedPhase)*0.00005;
        return float4(_BaseColor.rgb * value, 1);
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