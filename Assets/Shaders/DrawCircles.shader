Shader "Draw Circles"
{
    Properties{
    _CircleRadius("_CircleRadius", Float) = 0
    }
    
    Subshader
    {
        Pass
        {
            Cull Off
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
                fixed4 color    : COLOR;
                float2 uv : TEXCOORD0;
            };
            
            
            struct Circle
            {
                float3 position;
                float3 color;
            };
 
            StructuredBuffer<Circle> _ComputeBuffer;
            float _CircleRadius;
 
            float Mod(float x, float y)
            {
                return x - y * floor(x/y);
            }
            
            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4 pos = i.vertex ;
                pos *= _CircleRadius * 2;
                pos.z = 1;
                o.vertex = UnityObjectToClipPos(pos);
                o.vertex.xy += _ComputeBuffer[instanceID].position.xy;
                o.color = float4(_ComputeBuffer[instanceID].color,1);
                o.uv = i.uv;

                return o;
            }

            
            fixed4 frag(v2f i) : SV_Target {
                
                float2 s = i.uv * 2.0 - 1.0;
                float dis = abs(distance(s,0)) ;
                clip(1 - dis);
                return i.color;
            }
 
            float4 VSMain (uint id : SV_VertexID, out float2 uv : TEXCOORD0, inout uint instance : SV_INSTANCEID) : SV_POSITION
            {
                float3 center = _ComputeBuffer[instance].position;
                float u = sign(Mod(20.0, Mod(float(id), 6.0) + 2.0));
                float v = sign(Mod(18.0, Mod(float(id), 6.0) + 2.0));
                uv = float2(u,v);
                float4 position = float4(float3(sign(u) - 0.5, sign(v) - 0.5, 0.0) , 1.0);
                position *= _CircleRadius;
                position *= 0.5;
                position.z = 1;
                float4 clipPos = UnityObjectToClipPos(position);;
                clipPos.xy += center.xy;
                return clipPos;
            }
 
            float4 PSMain (float4 vertex : SV_POSITION, float2 uv : TEXCOORD0, uint instance : SV_INSTANCEID) : SV_Target
            {
                float2 s = uv * 2.0 - 1.0;
                float dis = abs(distance(s,0)) ;
                clip(_CircleRadius * 2);
                return float4(uv,0, 1.0);
            }
            ENDCG
        }
    }
}