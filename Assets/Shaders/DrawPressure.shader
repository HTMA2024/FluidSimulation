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
            float _Pixel;
            float4 _TexelSize;

 
            float Mod(float x, float y)
            {
                return x - y * floor(x/y);
            }
            
            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4 pos = i.vertex ;
                pos.xy *= _SmoothRadius * 2.f;
                pos.z = 1;
                pos.x *= _TexelSize.y/_TexelSize.x;
                o.vertex = pos;
                o.vertex.xy += _ComputeBuffer[instanceID].position.xy;
                o.screenPos = ComputeScreenPos(o.vertex);
                o.particleCenterPos = float4(_ComputeBuffer[instanceID].position.xy,0,0);
                o.uv = i.uv;

                return o;
            }

            
            fixed4 frag(v2f i) : SV_Target {

				float4 particleCenterPos = i.particleCenterPos * 0.5 + 0.5;
                particleCenterPos.y = 1 - particleCenterPos.y;
                
                float mass = 1;
                float2 s = i.uv * 2.0 - 1.0;
                s.y *= -1;
                float dis = abs(distance(s,0));
                float2 dir = dis <= 1e-5 ? GetRandomDir(_Time.y) : s/dis;
                fixed slope = SmoothingKernelDerivative(1, dis);

                float2 screenPosNorm = i.screenPos.xy/i.screenPos.w;
                float densitySelf = _FluidDensity.Sample(sampler_point_clamp, float4(particleCenterPos.xy,0,0));
                float densityOthers = _FluidDensity.Sample(sampler_point_clamp, float4(screenPosNorm.xy,0,0));
                
                float2 gradient = densitySelf < 1e-5 ? 0 : -dir * slope * mass / max(densitySelf,1e-5);
                float pressureSelf = ConvertDensityToPressure(densitySelf, _TargetValue, _PressureMultiplier);
                float pressureOthers = ConvertDensityToPressure(densityOthers, _TargetValue, _PressureMultiplier);
                float pressure = (pressureSelf + pressureOthers) / 2;
                float2 pressureForce = pressure * gradient;
                float4 res = dis < 1.f/(_Pixel * _SmoothRadius) ? 0.0 : float4(pressureForce,0,1);
				return res;
            }
 
            ENDCG
        }
    }
}