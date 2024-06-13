Shader "Viz Density"
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
            #pragma multi_compile_instancing
            #pragma target 5.0
            #include "UnityCG.cginc"
            #include "Assets/Shaders/ComputeShader/FluidParticle.hlsl"
 
            struct appdata_t {
                float4 vertex   : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f {
                float4 vertex   : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            StructuredBuffer<FluidParticlePhysics> _ComputeBuffer;
            float _SmoothRadius;
            sampler2D _FluidDensity;
            float4 _UnderTargetColor;
            float4 _OverTargetColor;
            float4 _AroundTargetColor;

 
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

            
            fixed4 frag(v2f i) : SV_Target
            {
                float res = tex2Dlod(_FluidDensity, float4(i.uv,0,0)).r;
                res = min(res,2);
				return   lerp(1, 0, res) * _UnderTargetColor + lerp(1, 0, abs(res - 1)) * _AroundTargetColor + lerp(0,1, res - 1) * _OverTargetColor;
            }
 
            ENDCG
        }
    }
}