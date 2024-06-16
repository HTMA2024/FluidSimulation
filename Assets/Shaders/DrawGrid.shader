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

            
            Buffer<int> _FluidParticleGrid;
            Buffer<int2> _FluidParticleGridSorted;
            StructuredBuffer<FluidParticlePhysics> _ComputeBuffer;
            float _FluidDeltaTime;
            float _SmoothRadius;
            float4 _TexelSize;
            int _FluidParticleCount;
            float4 _CursorPosition;
            int _Selector;

 
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
	            uint totalCount = (xCount) * (yCount);

                scaledPixelUV.x = (xCount * i.uv.x );
                scaledPixelUV.y = (yCount * i.uv.y);
                
                float2 grid = frac(scaledPixelUV);
                int id = floor(floor(scaledPixelUV.x) + floor(scaledPixelUV.y) * (xCount+1));
                
                grid.y = 1 - smoothstep(0.01,0.05,sin(grid.y*UNITY_PI));
                grid.x = 1 - smoothstep(0.01,0.05,sin(grid.x*UNITY_PI)) ;
                float gridStroke = saturate(grid.x + grid.y);

                // int pID = -1;
                // for(int j = 0; j < _FluidParticleCount; j++)
                // {
                //     if(_ComputeBuffer[j].gridID == id)
                //     {
                //         return  1;
                //     }
                // }

	            float2 cursorPos = _CursorPosition.xy;
	            int cGridIDX = floor(cursorPos.x * xCount);
	            int cGridIDY = floor(cursorPos.y * yCount);

                float4 vizColor = float4(0,0,0,1);
                vizColor.r += gridStroke;
                for(int j = -1; j <= 1; j++)
                {
                    for(int k = -1; k <= 1; k++)
                    {
                        int cIDX = cGridIDX + j;
                        int cIDY = cGridIDY + k;
                        if(cIDX < 0 || cIDX > xCount) continue;
                        if(cIDY < 0 || cIDY > yCount) continue;
                        int cID = floor(cIDX + cIDY * (xCount+1));
                        if(cID == id) // Get Closest 9 Grids
                        {
                            vizColor += float4(cID/((xCount)*(yCount)),0,0,1); // Viz Search Range
                            uint hashGridID = floor(hashwithoutsine11(cID) * totalCount);
                            
                            int sortedStartID = _FluidParticleGrid[hashGridID];
                            if(sortedStartID > _FluidParticleCount) continue;
                            int lightInGridStartID = _FluidParticleGridSorted[sortedStartID].y;
                            float2 startLightPos = _ComputeBuffer[lightInGridStartID].position * 0.5 + 0.5;
                            startLightPos.y = 1 -  startLightPos.y;

                            // Draw Circle On Found Light
                            uint loopID = sortedStartID;
                            
                            float2 pUV = i.uv - startLightPos.xy;
                            pUV.x *= _TexelSize.x / _TexelSize.y;
                            float disP = distance(pUV,0);
                            vizColor += disP < _SmoothRadius / 5.f ? float4(0,1,0,0) : 0;
                            loopID += 1;
                            
                            while(_FluidParticleGridSorted[loopID].x == _FluidParticleGridSorted[loopID - 1].x)
                            {
                                int lightInGridStartIDLoop = _FluidParticleGridSorted[loopID].y;
                                float2 startLightPosLoop = _ComputeBuffer[lightInGridStartIDLoop].position * 0.5 + 0.5;
                                startLightPosLoop.y = 1 -  startLightPosLoop.y;

                                float2 pUVLoop = i.uv - startLightPosLoop.xy;
                                pUVLoop.x *= _TexelSize.x / _TexelSize.y;
                                float disPLoop = distance(pUVLoop,0);
                                vizColor += disPLoop < _SmoothRadius / 5.f ? float4(0,1,0,0) : 0;
                                loopID += 1;
                            }
                            
                            // Validate
                            // int pGridIDX = floor(startLightPos.x * xCount);
                            // int pGridIDY = floor(startLightPos.y * yCount);
                            // int pID = floor(pGridIDX + pGridIDY * (xCount + 1));
                            // if(pID == cID)
                            // {
                            //     vizColor += float4(0,1,0,0);  // Viz Light list 
                            // }
                        }
                    }
                }

                // float output = pID == id ? 1 : gridStroke;
                float idListDisplay = id/((xCount)*(yCount));
                // return idListDisplay;
                return saturate(vizColor);
            }
 
            ENDCG
        }
    }
}