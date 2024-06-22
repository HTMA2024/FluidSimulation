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
	float density;
	float3 pressure;
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

float hashwithoutsine11(float p)
{
	p = frac(p * .1031);
	p *= p + 33.33;
	p *= p + p;
	return frac(p);
}

float2 GetRandomDir(float seed)
{
	float seed0 = seed;
	float seed1 = seed * 2;
	float hash0 = frac( ( sin(dot( seed0 , float2( 12.9898,78.233 ) * 43758.55 ) )) );
	float hash1 = frac( ( sin(dot( seed1 , float2( 12.9898,78.233 ) * 43758.55 ) )) );
	float2 dir =float2(hash0 * 2 -1,hash1 * 2 -1);
	dir = normalize(dir);
	return dir;
}

float CalculateDensity(float2 particlePos, float2 otherParticlePos, float2 texelSize, float smoothRadius)
{
	float mass = 1;
	float2 s = particlePos - otherParticlePos;
	s.y *= 2;
	s.x *= 2 * texelSize.x/texelSize.y;
	
	float2 dir = normalize(s);
	float dst = abs(distance(s,0));
	float influence = SmoothingKernel(smoothRadius, dst);
	float density = mass * influence;
	return density;
}

float4 CalculatePressure(float2 particlePos, float2 otherParticlePos,float densitySelf, float densityOthers, float2 texelSize, float smoothRadius, float targetValue, float pressureMultiplier, float seed)
{
	float mass = 1;
	float2 s = particlePos - otherParticlePos;
	s.y *= 2;
	s.x *= 2 * texelSize.x/texelSize.y;

	float dis = abs(distance(s,0));
	float2 dir = dis <= 1e-5 ? GetRandomDir(seed) : s/dis;
	float slope = SmoothingKernelDerivative(smoothRadius, dis);
                
	float2 gradient = -dir * slope * mass / max(densitySelf,1e-5);
	float pressureSelf = ConvertDensityToPressure(densitySelf, targetValue, pressureMultiplier);
	float pressureOthers = ConvertDensityToPressure(densityOthers, targetValue, pressureMultiplier);
	float pressure = (pressureSelf + pressureOthers) / 2;
	float2 pressureForce = pressure * gradient;
    float4 res = dis < 1e-4 ? 0.0 : float4(pressureForce,0,1);
                
	return res;
}

#endif // UNITY_CG_INCLUDED