#ifndef X_COMMON_INCLUDED
#define X_COMMON_INCLUDED

void ClipLOD(float2 positionCS, float fade)
{
#if defined(LOD_FADE_CROSSFADE)
    float dither = InterleavedGradientNoise(positionCS.xy, 0);
    clip(fade + (fade < 0.0 ? dither : -dither));
#endif
}

#endif
