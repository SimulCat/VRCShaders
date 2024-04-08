#ifndef PCG_HASH
#define PCG_HASH
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

#endif