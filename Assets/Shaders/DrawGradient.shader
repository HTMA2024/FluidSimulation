Shader "Draw Gradient"
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

            
            fixed4 frag(v2f i) : SV_Target
            {
				float4 particleCenterPos = i.particleCenterPos * 0.5 + 0.5;
                particleCenterPos.y = 1 - particleCenterPos.y;
                
                float mass = 1;
                float2 s = i.uv * 2.0 - 1.0;
                s.y *= -1;
                float dis = abs(distance(s,0));
                float2 dir = s/dis;
                fixed slope = SmoothingKernelDerivative(1, dis);

                
                // float density = tex2Dlod(_FluidDensity, float4(screenPosNorm.xy,0,0)); // SamplePoint Density
                float density = _FluidDensity.Sample(sampler_point_clamp, float4(particleCenterPos.xy,0,0));

                float2 gradient = density <  1e-5 ? 0 : dir * slope * mass / density;
                
				return float4(s.xy ,0,1);
            }
 
            ENDCG
        }
    }
}