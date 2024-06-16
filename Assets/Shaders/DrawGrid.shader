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
            int _FluidParticleCount;
            float4 _CursorPosition;

 
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
                float2 scaledPixelUV = 1;

                float yCount = floor(1 / _SmoothRadius);
                float xCount = floor((_TexelSize.x / _TexelSize.y) / _SmoothRadius);

                scaledPixelUV.x = (xCount * i.uv.x );
                scaledPixelUV.y = (yCount * i.uv.y);
                
                float2 grid = frac(scaledPixelUV);
                int id = floor(floor(scaledPixelUV.x) + floor(scaledPixelUV.y) * (xCount+1));
                
                grid.y = 1 - smoothstep(0.01,0.05,sin(grid.y*UNITY_PI));
                grid.x = 1 - smoothstep(0.01,0.05,sin(grid.x*UNITY_PI)) ;
                float gridStroke = saturate(grid.x + grid.y);

                int pID = -1;
                // for(int j = 0; j < _FluidParticleCount; j++)
                // {
                //     if(_ComputeBuffer[j].gridID == id)
                //     {
                //         return  1;
                //     }
                // }

	            float2 cursorPos = _CursorPosition.xy;
	            int pGridIDX = floor(cursorPos.x * xCount);
	            int pGridIDY = floor(cursorPos.y * yCount);
                for(int j = -1; j <= 1; j++)
                {
                    for(int k = -1; k <= 1; k++)
                    {
                        int gIDX = pGridIDX + j;
                        int gIDY = pGridIDY + k;
                        if(gIDX < 0 || gIDX > xCount) continue;
                        if(gIDY < 0 || gIDY > yCount) continue;
                        int gID = floor(gIDX + gIDY * (xCount+1));
                        if(gID == id) return 1;
                    }
                }

                // float output = pID == id ? 1 : gridStroke;
                // float idListDisplay = id/((xCount+1)*(yCount+1));
                // return idListDisplay;
                return gridStroke;
            }
 
            ENDCG
        }
    }
}