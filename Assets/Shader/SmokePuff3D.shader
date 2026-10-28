// The 3D smoke puffs (user, 2026-10-28: the flat 2D smoke looked wrong in VR): mesh particles drawn as soft round balls.
// Unlit, so they look the same in the dark rooms: the particle's own colour (start colour x colour over lifetime), lighter
// on top and darker underneath (a fixed light from above), with a light rim round the edge. See-through (user, 2026-10-28:
// "reduce opacity"): Opacity times the particle's own fade, and thinner towards the ball's edge (Edge Softness), so it
// reads as smoke rather than a solid ball. Fog like everything else; single-pass VR.
Shader "429/Particles/Smoke Puff 3D"
{
    Properties
    {
        _Opacity("Opacity", Range(0, 1)) = 0.55
        _EdgeSoftness("Edge Softness (thinner at the edge)", Range(0, 1)) = 0.6
        _LightDirection("Light From (world direction)", Vector) = (0.3, 1, 0.2, 0)
        _Top("Lit Side Brightness", Range(0, 2)) = 1.1
        _Bottom("Underside Brightness", Range(0, 1)) = 0.55
        _RimColor("Rim Colour", Color) = (1, 1, 1, 1)
        _RimStrength("Rim Strength", Range(0, 1)) = 0.35
        _RimPower("Rim Width (higher = thinner)", Range(0.5, 8)) = 2.5
        _Brightness("Brightness", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Sphere"
        }

        Pass
        {
            Name "SmokePuff"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LightDirection;
                half4 _RimColor;
                half _Top;
                half _Bottom;
                half _RimStrength;
                half _RimPower;
                half _Brightness;
                half _Opacity;
                half _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normalWS);
                // Lighter where it faces the light, darker underneath (wrapped, so the underside isn't black).
                half light = lerp(_Bottom, _Top, saturate(dot(normal, normalize(_LightDirection.xyz)) * 0.5 + 0.5));
                // A soft light rim where the ball turns away from the eye.
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = saturate(dot(normal, view));
                half rim = pow(1.0h - facing, _RimPower) * _RimStrength;
                half3 colour = input.color.rgb * light * _Brightness;
                colour = lerp(colour, _RimColor.rgb * max(input.color.rgb, 0.5h), rim);
                colour = MixFog(colour, input.fogFactor);
                // See-through: Opacity times its own fade, thinner where the ball turns away (its edge).
                half alpha = input.color.a * _Opacity * lerp(1.0h, facing, _EdgeSoftness);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
