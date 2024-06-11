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
            
            
            struct Particles
            {
                float3 position;
                // float3 color;
            };

            float4 _Color;
            StructuredBuffer<Particles> _ComputeBuffer;
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

            
            fixed4 frag(v2f i) : SV_Target {
                
                float2 s = i.uv * 2.0 - 1.0;
                float dis = abs(distance(s,0));
                fixed4 res = 1 - dis;
                // clip(res);
                return res * _Color;
            }
 
            ENDCG
        }
    }
}