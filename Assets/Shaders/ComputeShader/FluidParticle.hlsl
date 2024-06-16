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
	int gridID;
	float3 position;
	float3 velocity;
	float3 acceleration;
	float4 color;
};


float SmoothingKernel(float radius, float dst)
{
	if(dst >= radius) return 0;
	
	float volume = (UNITY_PI * pow(radius, 4.f)) /6.f;
	float value = (radius - dst) * (radius - dst) / volume;
	return value;
}

float SmoothingKernelDerivative(float radius, float dst)
{
	if(dst >= radius) return 0;

	float scale = 12 / (pow(radius,4.f) * UNITY_PI);
	return (dst - radius) * scale;
}

float ConvertDensityToPressure(float density, float targetDensity, float pressureMultiplier)
{
	float densityError = density - targetDensity;
	float pressure = densityError * pressureMultiplier; 
	return pressure;
}

#endif // UNITY_CG_INCLUDED