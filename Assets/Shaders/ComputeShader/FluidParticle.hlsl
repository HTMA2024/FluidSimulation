#ifndef FLUIDPARTICLE_INCLUDED
#define FLUIDPARTICLE_INCLUDED

#define UNITY_PI            3.14159265359f
#define UNITY_TWO_PI        6.28318530718f
#define UNITY_FOUR_PI       12.56637061436f
#define UNITY_INV_PI        0.31830988618f
#define UNITY_INV_TWO_PI    0.15915494309f
#define UNITY_INV_FOUR_PI   0.07957747155f
#define UNITY_HALF_PI       1.57079632679f
#define UNITY_INV_HALF_PI   0.636619772367f

struct FluidParticlePhysics
{
	int index;
	float3 position;
	float3 velocity;
	float3 acceleration;
};

struct FluidParticleGraphics
{
	float3 position;
	// float3 color;
};


float SmoothingKernel(float radius, float dst)
{
	float volume = UNITY_PI * pow(radius, 8) /4;
	float value = max(0, radius * radius - dst * dst);
	return value * value * value / volume;
}

float SmoothingKernelDerivative(float radius, float dst)
{
	if(dst >= radius) return 0;
	float f = radius * radius - dst * dst;
	float scale = -24/(UNITY_PI * pow(radius,8));
	return scale * dst * f * f;
}

#endif // UNITY_CG_INCLUDED