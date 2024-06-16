Shader "Draw Density"
{
    Properties{
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
            #pragma multi_compile_instancing
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
            };

            StructuredBuffer<FluidParticlePhysics> _ComputeBuffer;
            float _FluidDeltaTime;
            float _SmoothRadius;
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
                o.vertex.xy += _ComputeBuffer[instanceID].position.xy + _ComputeBuffer[instanceID].velocity.xy *_FluidDeltaTime;
                // o.color = float4(_ComputeBuffer[instanceID].color,1);
                o.uv = i.uv;

                return o;
            }

            
            float4 frag(v2f i) : SV_Target {
                
                float mass = 1;
                float2 s = i.uv * 2.0 - 1.0;
                float dis = abs(distance(s,0));
                // fixed4 res = max(0, 1 - dis);
                float influence = SmoothingKernel(1, dis);
                float density = mass * influence;
                return float4(density,0,0,1);
            }
 
            ENDCG
        }
    }
}