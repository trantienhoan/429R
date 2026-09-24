// Transparent variant of g_common (same lighting/shadow-receive maths, see g_common.shader):
//   alpha  = tex.a * Color.a * vertexColor.a
// Blend Mode (driven by TransparentBlendModeShaderGUI, which writes _SrcBlend/_DstBlend):
//   Alpha       = SrcAlpha, OneMinusSrcAlpha
//   Premultiply = One, OneMinusSrcAlpha (texture rgb already multiplied by its alpha)
//   Additive    = SrcAlpha, One
//   Multiply    = DstColor, Zero, rgb faded to white by alpha (_ALPHAMODULATE_ON)
// Renders in the Transparent queue with alpha blending and no depth write by default.
// No ShadowCaster / DepthOnly / DepthNormals passes: a see-through surface must not cast
// solid shadows or occlude in the depth prepass.
// All-float on purpose, same as g_common (2026-09-16 green-channel speckle).
Shader "My Town/g_common_transparent"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Tiling ("Tiling", Vector) = (1, 1, 0, 0)
        _Offset ("Offset", Vector) = (0, 0, 0, 0)

        [Header(Lighting)]
        _LitFactor ("Lit Factor", Range(0, 1)) = 0
        _ShadowColor ("Shadow Color", Color) = (0, 0.592, 1, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.381
        [Toggle(_RECEIVE_SHADOWS_OFF)] _RECEIVE_SHADOWS_OFF ("Receive Shadows Off", Float) = 0

        [Header(Surface)]
        [Enum(Alpha, 0, Premultiply, 1, Additive, 2, Multiply, 3)] _BlendMode ("Blend Mode", Float) = 0
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 5
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 10
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _Tiling;
            float4 _Offset;
            float _LitFactor;
            float4 _ShadowColor;
            float _ShadowStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ALPHAMODULATE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "g_common_lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 color : COLOR;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _Tiling.xy + _Offset.xy;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 base = tex.rgb * _Color.rgb;

                float diffuse;
                float shadowAtten;
                MainLightShading_float(input.positionWS, normalize(input.normalWS), diffuse, shadowAtten);

                float3 lit = base * lerp(1.0, diffuse, _LitFactor);
                float shadow = (1.0 - shadowAtten) * _ShadowStrength;
                float3 rgb = lerp(lit, lit * _ShadowColor.rgb, shadow) * input.color.rgb;
                float alpha = tex.a * _Color.a * input.color.a;

                rgb = MixFog(rgb, input.fogFactor);

                #if _ALPHAMODULATE_ON
                rgb = lerp(1.0, rgb, alpha);
                #endif

                return float4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    CustomEditor "Npu.MyTown.CommonFeature.TransparentBlendModeShaderGUI"
}
