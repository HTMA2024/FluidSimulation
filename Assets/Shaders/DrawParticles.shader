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
                float4 color    : COLOR;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f {
                float4 vertex   : SV_POSITION;
                // fixed4 color    : COLOR;
                float2 uv : TEXCOORD0;
            };
            
            float4 _Color;
            StructuredBuffer<FluidParticlePhysics> _ComputeBuffer;
            float _ParticleRadius;
 
            float Mod(float x, float y)
            {
                return x - y * floor(x/y);
            }
            
            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4 pos = i.vertex ;
                pos *= _ParticleRadius * 2;
                pos.z = 0.5;
                o.vertex = UnityObjectToClipPos(pos);
                o.vertex.xy += _ComputeBuffer[instanceID].position.xy;
                // o.color = float4(_ComputeBuffer[instanceID].color,1);
                o.uv = i.uv;

                return o;
            }

            
            fixed4 frag(v2f i) : SV_Target {
                
                float2 s = i.uv * 2.0 - 1.0;
                float dis = abs(distance(s,0)) ;
                clip(1 - dis);
                return 1;
            }
 
            ENDCG
        }
    }
}