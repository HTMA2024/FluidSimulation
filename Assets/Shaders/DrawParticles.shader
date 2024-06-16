Shader "Draw Particles"
{
    Properties{
    _ParticleRadius("Particle Radius", Float) = 0
    }
    
    Subshader
    {
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 5.0
            #include "Assets/Shaders/ComputeShader/FluidParticle.hlsl"
 
            struct appdata_t {
                float4 vertex   : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f {
                float4 vertex   : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
            };
            
            float4 _ParticleColor;
            StructuredBuffer<FluidParticlePhysics> _ComputeBuffer;
            float _ParticleRadius;
            float _Pixel;
            float4 _TexelSize;
 
            float Mod(float x, float y)
            {
                return x - y * floor(x/y);
            }
            
            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4 pos = i.vertex ;
                pos.xy *= _ParticleRadius * 2.f;
                pos.z = 1;
                pos.x *= _TexelSize.y/_TexelSize.x;
                o.vertex = pos;
                o.vertex.xy += _ComputeBuffer[instanceID].position.xy;
                o.color = _ComputeBuffer[instanceID].color;
                // o.color = float4(_ComputeBuffer[instanceID].color,1);
                o.uv = i.uv;

                return o;
            }

            
            fixed4 frag(v2f i) : SV_Target
            {
                float2 s = i.uv * 2.0 - 1.0;
                float dis = abs(distance(s,0)) ;
                clip(1 - dis);
                float4 res = _ParticleColor;
                return res;
            }
 
            ENDCG
        }
    }
}