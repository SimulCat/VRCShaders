float saturate(float x)
{
    return min(1.0, max(0.0, x));
}

float3 saturate(float3 x)
{
    return min(float3(1., 1., 1.), max(float3(0., 0., 0.), x));
}

            // --- Spectral Zucconi 6 --------------------------------------------
            // Based on GPU Gems
            // Optimised by Alan Zucconi
            // --- Spectral Zucconi --------------------------------------------
            // By Alan Zucconi
            // Based on GPU Gems: https://developer.nvidia.com/sites/all/modules/custom/gpugems/books/GPUGems/gpugems_ch08.html
            // But with values optimised to match as close as possible the visible spectrum
            // Fits this: https://commons.wikimedia.org/wiki/File:Linear_visible_spectrum.svg
            // With weighter MSE (RGB weights: 0.3, 0.59, 0.11)
float3 bump3y(float3 x, float3 yoffset)
{
    float3 y = float3(1., 1., 1.) - x * x;
    y = saturate(y - yoffset);
    return y;
}
            
float3 spectral_frac(float x)
{
	            // x: [0,   1] (UV-RED)

    const float3 c1 = float3(3.54585104, 2.93225262, 2.41593945);
    const float3 x1 = float3(0.69549072, 0.49228336, 0.27699880);
    const float3 y1 = float3(0.02312639, 0.15225084, 0.52607955);

    const float3 c2 = float3(3.90307140, 3.21182957, 3.96587128);
    const float3 x2 = float3(0.11748627, 0.86755042, 0.66077860);
    const float3 y2 = float3(0.84897130, 0.88445281, 0.73949448);

    return bump3y(c1 * (x - x1), y1) + bump3y(c2 * (x - x2), y2);
}

float3 spectral_zucconi6(float w)
{
	            // w: [400, 700]
	            // x: [0,   1]
    float x = saturate((w - 400.0) / 300.0);
    return spectral_frac(x);
}
