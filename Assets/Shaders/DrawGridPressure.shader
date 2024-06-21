Shader "Draw Grid Pressure"
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
            
            UNITY_DECLARE_TEX2D(_FluidDensity);
            SamplerState sampler_point_clamp;
            float _TargetValue;
            float _PressureMultiplier;
            float _Pixel;

 
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
            
            float2 GetRandomDir(float seed)
            {
                float seed0 = seed;
                float seed1 = seed * 2;
			    float hash0 = frac( ( sin(dot( seed0 , float2( 12.9898,78.233 ) * 43758.55 ) )) );
                float hash1 = frac( ( sin(dot( seed1 , float2( 12.9898,78.233 ) * 43758.55 ) )) );
                float2 dir =float2(hash0 * 2 -1,hash1 * 2 -1);
                dir = normalize(dir);
                return dir;
            }

            float CalculateDensity(float2 uv, float2 lightPos)
            {
                float mass = 1;
                float2 s = uv - lightPos;
                s.y *= 2;
                s.x *= 2 * _TexelSize.x/_TexelSize.y;
                float2 dir = normalize(s);
                float dst = abs(distance(s,0));
                float influence = SmoothingKernel(_SmoothRadius, dst);
                float density = mass * influence;
                return density;
            }
            
            float4 CalculatePressure(float2 uv, float2 lightPos)
            {
                
                float mass = 1;
                float2 s = uv - lightPos;
                s.y *= 2;
                s.x *= 2 * _TexelSize.x/_TexelSize.y;

                float dis = abs(distance(s,0));
                float2 dir = dis <= 1e-5 ? GetRandomDir(_Time.y) : s/dis;
                fixed slope = SmoothingKernelDerivative(_SmoothRadius, dis);
                uint2 uvIndex = floor(float2(uv.x * _TexelSize.x, uv.y * _TexelSize.y));
                uint2 uvLightIndex = floor(float2(lightPos.x * _TexelSize.x, lightPos.y * _TexelSize.y));
                float densitySelf = _FluidDensity[uvIndex]; // Need to be changed
                float densityOthers = _FluidDensity[uvLightIndex]; // Need to be changed
                
                float2 gradient = -dir * slope * mass / max(densitySelf,1e-5);
                float pressureSelf = ConvertDensityToPressure(densitySelf, _TargetValue, _PressureMultiplier);
                float pressureOthers = ConvertDensityToPressure(densityOthers, _TargetValue, _PressureMultiplier);
                float pressure = (pressureSelf + pressureOthers) / 2;
                float2 pressureForce = pressure * gradient;
                float4 res = dis < 1e-3 ? 0.0 : float4(pressureForce,0,1);
                
                return res;
            }

            float4 CalculatePressureSearch(float2 uv)
            {
                float4 vizColor = 0;
                
                float yCount = floor(1 / _SmoothRadius);
                float xCount = floor((_TexelSize.x / _TexelSize.y) / _SmoothRadius);
	            uint totalCount = (xCount) * (yCount);
                
                int idX = floor(uv.x * xCount);
                int idY = floor(uv.y * yCount);
                int id = floor(idX + idY * (xCount));
                
                for(int j = -1; j <= 1; j++)
                {
                    for(int k = -1; k <= 1; k++)
                    {
                        int cIDX = idX + j;
                        int cIDY = idY + k;
                        if(cIDX < 0 || cIDX > xCount-1) continue;
                        if(cIDY < 0 || cIDY > yCount-1) continue;
                        int cID = floor(cIDX + cIDY * (xCount));
                        
                        uint hashGridID = cID;
                        int sortedStartID = _FluidParticleGrid[hashGridID];
                        if(sortedStartID > _FluidParticleCount) continue;
                        int lightInGridStartID = _FluidParticleGridSorted[sortedStartID].y;

                        // First Light Pos
                        float2 startLightPos = (_ComputeBuffer[lightInGridStartID].position + _ComputeBuffer[lightInGridStartID].velocity *_FluidDeltaTime) * 0.5 + 0.5;
                        startLightPos.y = 1 -  startLightPos.y;

                        uint loopID = sortedStartID;
                        float4 pressure = CalculatePressure(uv, startLightPos);
                        vizColor += pressure;
                        loopID += 1;

                        while(_FluidParticleGridSorted[loopID].x == _FluidParticleGridSorted[loopID - 1].x)
                        {
                            int lightInGridStartIDLoop = _FluidParticleGridSorted[loopID].y;
                            float2 startLightPosLoop = (_ComputeBuffer[lightInGridStartIDLoop].position + _ComputeBuffer[lightInGridStartIDLoop].velocity *_FluidDeltaTime)* 0.5 + 0.5;
                            startLightPosLoop.y = 1 -  startLightPosLoop.y;
                        
                            pressure = CalculatePressure(uv, startLightPosLoop);
                            vizColor += pressure;
                            loopID += 1;
                        }
                    }
                }
                return vizColor;
            }

            
            float4 frag(v2f i) : SV_Target
            {
                // int pID = -1;
                // for(int j = 0; j < _FluidParticleCount; j++)
                // {
                //     if(_ComputeBuffer[j].gridID == id)
                //     {
                //         return  1;
                //     }
                // }

	            // float2 cursorPos = _CursorPosition.xy;
                // float4 vizColor = VizSearchLight(i.uv, cursorPos);

                float4 pressure = CalculatePressureSearch(i.uv);

                uint2 uvIndex = floor(float2(i.uv.x * _TexelSize.x, i.uv.y * _TexelSize.y));
                float densitySelf = _FluidDensity[uvIndex]; // Need to be changed

                // float output = pID == id ? 1 : gridStroke;
                // return idListDisplay;
                return pressure;
            }
 
            ENDCG
        }
    }
}