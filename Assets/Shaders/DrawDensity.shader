Shader "Draw Density"
{
    Properties{
    _DensityRadius("Density Radius", Float) = 0
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
            };

            float4 _Color;
            StructuredBuffer<FluidParticlePhysics> _ComputeBuffer;
            float _DensityRadius;

 
            float Mod(float x, float y)
            {
                return x - y * floor(x/y);
            }
            
            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4 pos = i.vertex ;
                pos *= _DensityRadius * 2;
                pos.z = 1;
                o.vertex = UnityObjectToClipPos(pos);
                o.vertex.xy += _ComputeBuffer[instanceID].position.xy;
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
                return density;
            }
 
            ENDCG
        }
    }
}