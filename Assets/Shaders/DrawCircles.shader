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
            #pragma vertex VSMain
            #pragma fragment PSMain
            #pragma target 5.0
 
            struct Circle
            {
                float3 Position;
                float3 Color;
            };
 
            StructuredBuffer<Circle> _ComputeBuffer;
            float _CircleRadius;
 
            float Mod(float x, float y)
            {
                return x - y * floor(x/y);
            }
 
            float4 VSMain (uint id : SV_VertexID, out float2 uv : TEXCOORD0, inout uint instance : SV_INSTANCEID) : SV_POSITION
            {
                float3 center = _ComputeBuffer[instance].Position;
                float u = sign(Mod(20.0, Mod(float(id), 6.0) + 2.0));
                float v = sign(Mod(18.0, Mod(float(id), 6.0) + 2.0));
                uv = float2(u,v);
                float4 position = float4(float3(sign(u) - 0.5, sign(v) - 0.5, 0.0) + center, 1.0);
                return UnityObjectToClipPos(position);
            }
 
            float4 PSMain (float4 vertex : SV_POSITION, float2 uv : TEXCOORD0, uint instance : SV_INSTANCEID) : SV_Target
            {
                float2 s = uv * 2.0 - 1.0;
                float dis = abs(distance(s,0)) ;
                clip(_CircleRadius - dis);
                return float4(_ComputeBuffer[instance].Color, 1.0);
            }
            ENDCG
        }
    }
}