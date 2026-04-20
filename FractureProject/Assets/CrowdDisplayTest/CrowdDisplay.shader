Shader "Custom/CrowdDisplay"
{
    Properties
    {
        _MainTex ("Sprite Atlas Texture", 2D) = "white" {}
        _Scale ("Taille (X=Largeur, Y=Hauteur)", Vector) = (1, 1, 0, 0)
        _Width ("Largeur de la foule", Float) = 2.0
        _BounceSpeed ("Bounce Speed", Float) = 5
        _BounceAmp ("Bounce Amplitude", Float) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct CharacterData {
                float3 randomOffset;
                float absoluteDistance;
                float4 uvRect;
                int pathIndex;
                float3 _padding;
            };

            struct PathInfo {
                int waypointCount;
                float totalLength;
                float2 _padding;
            };

            StructuredBuffer<CharacterData> _CrowdBuffer;
            StructuredBuffer<float4> _AllPathsBuffer;
            StructuredBuffer<PathInfo> _PathInfoBuffer;

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
                float _GlobalOffset;
                float _BounceSpeed;
                float _BounceAmp;
                int _PathCount;
            CBUFFER_END

            Varyings vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                CharacterData data = _CrowdBuffer[instanceID];
                PathInfo path = _PathInfoBuffer[data.pathIndex];

                // GlobalOffset uniquement sur le chemin actif
                float globalOff = (data.pathIndex == 0) ? _GlobalOffset : 0.0;
                float absolutePos = data.absoluteDistance + globalOff;

                // Chemin actif : boucle / Chemin historique : avance jusqu'au bout
                float progress;
                if (data.pathIndex == 0)
                    progress = frac(absolutePos / path.totalLength);
                else
                    progress = saturate(absolutePos / path.totalLength);

                float targetDistance = progress * path.totalLength;

                // Recherche du segment dans le buffer partagé
                int segmentIndex = 0;
                float localProgress = 0.0;

                for (int i = 0; i < path.waypointCount - 1; i++)
                {
                    float distStart = _AllPathsBuffer[i].w;
                    float distEnd   = _AllPathsBuffer[i + 1].w;

                    if (targetDistance >= distStart && targetDistance <= distEnd)
                    {
                        segmentIndex = i;
                        float segLen = distEnd - distStart;
                        localProgress = (targetDistance - distStart) / max(0.001, segLen);
                        break;
                    }
                }

                float3 pointA = _AllPathsBuffer[segmentIndex].xyz;
                float3 pointB = _AllPathsBuffer[segmentIndex + 1].xyz;

                float3 basePos = lerp(pointA, pointB, localProgress);

                float3 dir = normalize(pointB - pointA);
                float3 up = float3(0, 1, 0);
                float3 sideDir = normalize(cross(dir, up));
                if (length(sideDir) < 0.01) sideDir = float3(1, 0, 0);

                float3 sideOffset = sideDir * (data.randomOffset.x * _Width);
                float3 worldPos = basePos + sideOffset;

                float bounce = abs(sin(_Time.y * _BounceSpeed + data.randomOffset.y)) * _BounceAmp;
                worldPos.y += bounce;

                float3 scaledPositionOS = input.positionOS.xyz * _Scale.xyz;
                output.positionCS = TransformWorldToHClip(worldPos + scaledPositionOS);

                output.uv = input.uv * data.uvRect.zw + data.uvRect.xy;

                // Fade sur chemin actif, toujours visible sur chemin historique
                if (data.pathIndex == 0)
                    output.alpha = smoothstep(0.0, 0.05, progress) * (1.0 - smoothstep(0.95, 1.0, progress));
                else
                    output.alpha = 1.0;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                col.a *= input.alpha;
                clip(col.a - 0.1);
                return col;
            }
            ENDHLSL
        }
    }
}