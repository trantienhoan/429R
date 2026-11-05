// The paint on a hand dipped in a Paint Bees bucket (user, 2026-11-05: "make part of my palm that put in the bucket that
// colour"). Drawn on a copy of the hand's skinned renderer (same mesh, same bones), puffed out a hair along its normals:
// only the part of the hand below the paint line shows, the line kept in the hand's own space (_CoatToLocal: world to
// the hand, no scale, metres; _CoatPlane: xyz = the way into the paint in that space, w = the line's distance along it),
// so it stays on the same part of the hand as it moves. Lit like the bucket's paint: ambient, main light, a sharp wet
// highlight and a little of its own light; soft edge at the line; fog; single-pass VR.
Shader "429/Paint Coat"
{
    Properties
    {
        [MainColor] _BaseColor("Paint", Color) = (1, 0.2, 0.2, 1)
        _CoatPlane("Paint Line (hand space: into the paint xyz, distance w)", Vector) = (0, -1, 0, 1000)
        _Edge("Soft Edge (m)", Range(0.001, 0.03)) = 0.006
        _Puff("Puff Out (m)", Range(0, 0.01)) = 0.0012
        _Shine("Wet Highlight", Range(0, 3)) = 1.1
        _SelfLit("Own Light", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PaintCoat"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _CoatPlane;
                float _Edge;
                float _Puff;
                half _Shine;
                half _SelfLit;
            CBUFFER_END

            // World to the hand's own space (set per hand by Paint Hands).
            float4x4 _CoatToLocal;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float depth : TEXCOORD2;     // metres below the paint line (negative: above it, dry)
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 local = mul(_CoatToLocal, float4(positionWS, 1.0)).xyz;
                output.depth = dot(_CoatPlane.xyz, local) - _CoatPlane.w;
                positionWS += normalWS * _Puff;
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half cover = smoothstep(-_Edge, _Edge, input.depth) * _BaseColor.a;
                clip(cover - 0.01);
                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light sun = GetMainLight();
                half3 colour = _BaseColor.rgb;
                half3 lit = colour * (SampleSH(n) + sun.color * saturate(dot(n, sun.direction)) * 0.8 + _SelfLit);
                half3 h = normalize(sun.direction + v);
                lit += sun.color * pow(saturate(dot(n, h)), 90.0) * _Shine;
                lit += pow(1.0 - saturate(dot(n, v)), 4.0) * 0.25 * (colour * 0.5 + 0.5);
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, cover);
            }
            ENDHLSL
        }
    }
}
