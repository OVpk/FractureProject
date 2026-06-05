Shader "Custom/SymbolDisplay"
{
    Properties
    {
        _MainTex ("Atlas des Symboles", 2D) = "white" {}
        _Scale ("Taille (X=Largeur, Y=Hauteur)", Vector) = (1, 1, 0, 0)
        _Width ("Dispersion sur la largeur", Float) = 2.0
        _BaseYOffset ("Ajustement vertical global", Float) = -1.0 
        _DepthBias ("Rapprochement Caméra", Float) = 2.0 
        _RotationY ("Rotation Y", Float) = 0.0
        
        [Header(Generation Settings)]
        _Density ("Densité des symboles", Range(0.0, 1.0)) = 1.0

        _Jitter ("Désordre d'espacement (Jitter)", Float) = 1.5
        
        [Header(Animation Settings)]
        _CycleDuration ("Durée totale de la boucle (sec)", Float) = 5.0
        _RiseDuration ("Temps de montée (sec)", Float) = 0.5
        _HoldDuration ("Temps de pause en haut (sec)", Float) = 1.0
        _FadeDuration ("Temps de fondu (sec)", Float) = 0.5
        _FloatHeight ("Hauteur d'élévation maximale", Float) = 2.0
        
        [Header(Scale Animation)]
        _ScaleRiseDuration ("Temps de croissance taille (sec)", Float) = 0.3
        _PulseDelay ("Délai avant pulsation (sec)", Float) = 0.5
        _PulseFrequency ("Vitesse de pulsation", Float) = 8.0
        _PulseAmplitude ("Force de pulsation", Float) = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off 

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct SymbolData {
                float3 randomOffset; 
                float absoluteDistance;
                float4 uvRect;
            };

            StructuredBuffer<SymbolData> _SymbolBuffer;
            StructuredBuffer<float4> _WaypointBuffer;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Scale;
                float _Width;
                float _BaseYOffset; 
                float _DepthBias; 
                int _WaypointCount;
                float _TotalPathLength;
                float _RotationY;
                
                float _Density; 
                float _Jitter;

                float _ResumeTime;
                
                float _CycleDuration;
                float _RiseDuration;
                float _HoldDuration;
                float _FadeDuration;
                float _FloatHeight;
                
                float _ScaleRiseDuration;
                float _PulseDelay;
                float _PulseFrequency;
                float _PulseAmplitude;
            CBUFFER_END

            float rand(float2 co) {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }

            Varyings vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                SymbolData data = _SymbolBuffer[instanceID];

                float localTime = _Time.y + data.randomOffset.z;
                float cycleIndex = floor(localTime / max(0.001, _CycleDuration));
                float timeInCycle = localTime - (cycleIndex * _CycleDuration); 
                
                float activeDuration = _RiseDuration + _HoldDuration + _FadeDuration;

                float globalCycleStart = _Time.y - timeInCycle;

                float2 seed = float2((float)instanceID, cycleIndex);
                float randomDensity = rand(seed + float2(1.1, 2.2));

                if (randomDensity > _Density || timeInCycle > activeDuration || data.uvRect.z == 0.0 || globalCycleStart < _ResumeTime)
                {
                    output.positionCS = float4(0, 0, 0, 0);
                    output.uv = float2(0, 0);
                    output.alpha = 0.0;
                    return output;
                }


                float cycleOffset = rand(float2(cycleIndex, 99.99));
                float goldenRatio = 0.61803398875;
                float uniformProgress = frac((float)instanceID * goldenRatio + cycleOffset);
                float baseTargetDistance = uniformProgress * _TotalPathLength; 
                
                float distJitter = (rand(seed + float2(3.3, 4.4)) - 0.5) * _Jitter;
                float targetDistance = clamp(baseTargetDistance + distJitter, 0.0, _TotalPathLength);
                
                float sideSign = (instanceID % 2 == 0) ? 1.0 : -1.0;
                float randomLatFactor = rand(seed + float2(5.5, 6.6));

                float lateralOffsetDist = sideSign * lerp(0.1, 1.0, randomLatFactor) * _Width; 
                
                float randomHeightFactor = rand(seed + float2(7.7, 8.8));
                float baseHeight = lerp(1.5, 3.0, randomHeightFactor); 


                float heightProgress = saturate(timeInCycle / max(0.001, _RiseDuration));
                
                float timeBeforeFade = _RiseDuration + _HoldDuration;
                float fadeProgress = saturate((timeInCycle - timeBeforeFade) / max(0.001, _FadeDuration));
                output.alpha = 1.0 - fadeProgress;

                float growProgress = saturate(timeInCycle / max(0.001, _ScaleRiseDuration));
                float pulseTime = max(0.0, timeInCycle - _PulseDelay);
                float pulse = (-cos(pulseTime * _PulseFrequency) * 0.5 + 0.5) * _PulseAmplitude;
                float scaleMultiplier = growProgress * (1.0 + pulse);

                int segmentIndex = 0;
                float localProgress = 0.0;
                
                if (targetDistance <= 0.0) 
                {
                    segmentIndex = 0;
                    localProgress = 0.0;
                }
                else if (targetDistance >= _TotalPathLength)
                {
                    segmentIndex = _WaypointCount - 2;
                    localProgress = 1.0;
                }
                else 
                {
                    for(int i = 0; i < _WaypointCount - 1; i++) 
                    {
                        float distStart = _WaypointBuffer[i].w;
                        float distEnd = _WaypointBuffer[i+1].w;
                        
                        if(targetDistance >= distStart && targetDistance <= distEnd) 
                        {
                            segmentIndex = i;
                            float segmentLength = distEnd - distStart;
                            localProgress = (targetDistance - distStart) / max(0.001, segmentLength);
                            break;
                        }
                    }
                }

                float3 pointA = _WaypointBuffer[segmentIndex].xyz;
                float3 pointB = _WaypointBuffer[segmentIndex + 1].xyz;
                float3 basePos = lerp(pointA, pointB, localProgress);
                
                float3 dir = normalize(pointB - pointA);
                float3 up = float3(0, 1, 0);
                float3 sideDir = normalize(cross(dir, up));
                if(length(sideDir) < 0.01) sideDir = float3(1, 0, 0);
                
                float3 sideOffset = sideDir * lateralOffsetDist;
                float3 worldPos = basePos + sideOffset;

                worldPos.y += baseHeight + (heightProgress * _FloatHeight) + _BaseYOffset;

                float3 scaledPositionOS = input.positionOS.xyz * _Scale.xyz * scaleMultiplier;
                
                float radY = radians(_RotationY);
                float cosY = cos(radY);
                float sinY = sin(radY);
                
                float xRot = scaledPositionOS.x * cosY - scaledPositionOS.z * sinY;
                float zRot = scaledPositionOS.x * sinY + scaledPositionOS.z * cosY;
                
                scaledPositionOS.x = xRot;
                scaledPositionOS.z = zRot;

                float3 finalWorldPos = worldPos + scaledPositionOS; 
                
                float3 camForward = GetViewForwardDir();
                finalWorldPos -= camForward * _DepthBias;
                
                output.positionCS = TransformWorldToHClip(finalWorldPos);
                output.uv = input.uv * data.uvRect.zw + data.uvRect.xy;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                col.a *= input.alpha;
                
                clip(col.a - 0.01); 
                return col;
            }
            ENDHLSL
        }
    }
}