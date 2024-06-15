Shader "Draw Pressure"
{
    Properties{
    _SmoothRadius("Density Radius", Float) = 0
    }
    
    Subshader
    {
        Pass
        {
            Tags { "Queue" = "Transparent" }
            
            Cull Off
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            #include "Assets/Shaders/ComputeShader/FluidParticle.hlsl"
 
            struct appdata_t {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f {
                float4 vertex   : SV_POSITION;
                // fixed4 color    : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 particleCenterPos : TEXCOORD2;
            };

            StructuredBuffer<FluidParticlePhysics> _ComputeBuffer;
            float _SmoothRadius;
            UNITY_DECLARE_TEX2D(_FluidDensity);
            SamplerState sampler_point_clamp;
            float _TargetValue;
            float _PressureMultiplier;

 
            float Mod(float x, float y)
            {
                return x - y * floor(x/y);
            }
            
            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4 pos = i.vertex ;
                pos *= _SmoothRadius * 2;
                pos.z = 1;
                o.vertex = UnityObjectToClipPos(pos);
                o.vertex.xy += _ComputeBuffer[instanceID].position.xy;
                o.screenPos = ComputeScreenPos(o.vertex);
                o.particleCenterPos = float4(_ComputeBuffer[instanceID].position.xy,0,0);
                o.uv = i.uv;

                return o;
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

            
            fixed4 frag(v2f i) : SV_Target {

				float4 particleCenterPos = i.particleCenterPos * 0.5 + 0.5;
                particleCenterPos.y = 1 - particleCenterPos.y;
                
                float mass = 1;
                float2 s = i.uv * 2.0 - 1.0;
                float dis = abs(distance(s,0));
                float2 dir = dis <= 1e-5 ? GetRandomDir(_Time.y) : s/dis;
                fixed slope = SmoothingKernelDerivative(1, dis);

                float2 screenPosNorm = i.screenPos.xy/i.screenPos.w;
                float densitySelf = _FluidDensity.Sample(sampler_point_clamp, float4(particleCenterPos.xy,0,0));
                float densityOthers = _FluidDensity.Sample(sampler_point_clamp, float4(screenPosNorm.xy,0,0));
                
                float2 gradient = -dir * slope * mass / max(densitySelf,1e-5);
                float pressureSelf = ConvertDensityToPressure(densitySelf, _TargetValue, _PressureMultiplier);
                float pressureOthers = ConvertDensityToPressure(densityOthers, _TargetValue, _PressureMultiplier);
                float pressure = (pressureSelf + pressureOthers) / 2;
                float2 pressureForce = pressure * gradient;
                float4 res = dis <= 1e-5 ? 0.0 : float4(pressureForce,0,1);
				return res;
            }
 
            ENDCG
        }
    }
}