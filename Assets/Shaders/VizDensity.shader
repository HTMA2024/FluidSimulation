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

            UNITY_DECLARE_TEX2D(_FluidDensity);
            SamplerState sampler_point_clamp;
            StructuredBuffer<FluidParticlePhysics> _ComputeBuffer;
            float _SmoothRadius;
            // sampler2D _FluidDensity;
            float4 _UnderTargetColor;
            float4 _OverTargetColor;
            float4 _AroundTargetColor;
            float _TargetValue;

 
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

            float3 getColorFromValue(float value, float targetValue, float3 underTargetColor, float3 aroundTargetColor, float3 overTargetColor) {
                float transitionRange = 0.1; // Adjust this value to control the width of the transition range
                
                float underTargetFactor = smoothstep(targetValue - transitionRange, 0,  value);
                float aroundTargetFactor = smoothstep(targetValue - transitionRange , 0, abs(value-targetValue));
                float overTargetFactor = smoothstep(targetValue + transitionRange, targetValue * 2, value);
                
                return underTargetColor * underTargetFactor + aroundTargetFactor *_AroundTargetColor + overTargetFactor * overTargetColor;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float value = _FluidDensity.Sample(sampler_point_clamp, float4(i.uv,0,0));
                value = max(0,value);
                float4 res = 1;
                res.rgb = getColorFromValue(value,_TargetValue,_UnderTargetColor,_AroundTargetColor,_OverTargetColor); 
				return res;
            }
 
            ENDCG
        }
    }
}