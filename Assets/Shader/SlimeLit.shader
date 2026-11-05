// The flying paint slime's skin (user 2026-11-05): lit like URP Lit (main light and its shadows, the other lights,
// light probes, fog), but only the painted parts take the slime's paint colour. Its texture's RGB is the paint (white
// shades where it takes the colour, the stem, collar, face and eyes as they are) and its alpha says where (1 = takes
// the Paint Colour). Rainbow: the painted parts go round the rainbow instead, in stripes round the slime that turn
// slowly. Glow: the Glow Map times the Glow Colour (the eyes), plus the painted parts glowing a little of their own
// colour (Paint Glow), so they read in the dark rooms. The Flying Slime script sets Paint Colour, Paint Glow, Rainbow
// and Glow Colour on each slime at runtime.
Shader "429/Slime Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Paint (RGB), takes the colour (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Paint Colour", Color) = (0.89, 0.15, 0.18, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.86
        _Metallic("Metallic", Range(0, 1)) = 0
        [NoScaleOffset] _EmissionMap("Glow Map", 2D) = "black" {}
        [HDR] _EmissionColor("Glow Colour", Color) = (1.6, 1.6, 1.6, 1)
        _PaintGlow("Paint Glow", Range(0, 1)) = 0.14
        [Toggle] _Rainbow("Rainbow", Float) = 0
        _RainbowSpeed("Rainbow Speed (turns a second)", Float) = 0.1
        _RainbowStripes("Rainbow Waves (per metre up)", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            half _Metallic;
            half4 _EmissionColor;
            half _PaintGlow;
            half _Rainbow;
            half _RainbowSpeed;
            half _RainbowStripes;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half4 fogAndVertexLight : TEXCOORD4;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // A hue (0..1) as a full colour.
            half3 Hue(half h)
            {
                return saturate(abs(frac(h + half3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogAndVertexLight = half4(ComputeFogFactor(position.positionCS.z), VertexLighting(position.positionWS, normal.normalWS));
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(position);
            #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 paint = _BaseColor.rgb;
                if (_Rainbow > 0.5)
                {
                    // Round the slime (its own up axis), waving up and down it, turning slowly.
                    float3 p = input.positionOS;
                    float round01 = atan2(p.x, p.z) * (0.5 / PI);
                    float h = round01 + 0.08 * sin(p.y * _RainbowStripes * 6.2832) + _Time.y * _RainbowSpeed;
                    paint = (Hue((half)frac(h)) * 0.82 + 0.18) * _BaseColor.rgb;
                }
                half mask = tex.a;
                half3 albedo = tex.rgb * lerp(half3(1.0, 1.0, 1.0), paint, mask);
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv).rgb * _EmissionColor.rgb + albedo * (mask * _PaintGlow);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogAndVertexLight.x);
                inputData.vertexLighting = input.fogAndVertexLight.yzw;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = _Metallic;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.emission = emission;
                surface.occlusion = 1.0;
                surface.alpha = 1.0;

                half4 colour = UniversalFragmentPBR(inputData, surface);
                colour.rgb = MixFog(colour.rgb, inputData.fogCoord);
                colour.a = 1.0;
                return colour;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(Attributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
