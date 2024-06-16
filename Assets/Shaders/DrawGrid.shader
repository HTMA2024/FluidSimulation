Shader "Draw Grid"
{
    Properties{
    }
    
    Subshader
    {
        Pass
        {
            Tags { "Queue" = "Transparent" }
            
            Cull Off
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
                o.vertex = UnityObjectToClipPos(pos);
                o.uv = i.uv;

                return o;
            }

            
            float4 frag(v2f i) : SV_Target
            {
                float2 pixelUV = i.uv ;
                float2 grid = frac(pixelUV / _SmoothRadius);
                grid = 1-smoothstep(0.05,0.1,sin(grid*UNITY_PI));
                float gridStroke = saturate(grid.x + grid.y);
                
                return 0;
            }
 
            ENDCG
        }
    }
}