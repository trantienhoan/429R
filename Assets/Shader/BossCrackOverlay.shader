// Cracks drawn over the pumpkin boss's giant pumpkin butt (user, 2026-11-11: the player's hits crack it, then it bursts;
// "that crack looks ugly": realistic pumpkin cracks, not lava). An overlay: the pumpkin's own material is drawn as it is and
// this goes on top (the boss puts it on the pumpkin's slot), see-through, no depth written, pulled a hair towards the eye
// (Offset) so it doesn't flicker against the pumpkin.
// Where: a cylinder round the pumpkin, worked out from each vertex's REST position (the model's import puts it in the
// mesh's fourth UV channel: Pumpkin Boss Butt Split), so the cracks are stuck to the pumpkin, seamless (the wrap is at its
// back) and never swim when it squashes or the hips are held: x = the angle round it (0.5 = its front), y = its height
// (Crack Axis: x, z = its middle, z, w = its bottom and top, model units).
// What: the Crack Map (Pumpkin Boss Slide Setup / Rebuild Boss Cracks make it): R = the Crack Amount at which a crack has
// grown to this spot (1 = never), G = how far from that crack's middle line (0..1, a distance field: sharp at any size),
// B = where its lip shows. As Crack Amount rises (0..1), cracks grow along their lines and widen from a hairline to their
// full width: a dark core (near black down the middle, deep brown at its edges), a thin pale lip of pumpkin flesh here and
// there, a faint shadow round them; close to the break a faint warm tint deep inside the widest. Lit simply (ambient and
// the main light). Collapse on, or Crack Amount 0: it draws nothing at all (every vertex outside the view). Cheap: one
// texture read and a little maths, only on the pumpkin's own pixels, only once it has cracks. Fog; single-pass VR.
Shader "429/Boss/Crack Overlay"
{
    Properties
    {
        _CrackAmount("Crack Amount", Range(0, 1)) = 0
        [NoScaleOffset] _CrackMap("Crack Map (R appears at, G from the middle, B lip)", 2D) = "white" {}
        _CrackAxis("Crack Axis (middle x, middle z, bottom y, top y)", Vector) = (0.01, 0, -0.02, 1.27)
        _CoreColor("Core (middle)", Color) = (0.03, 0.015, 0.008, 1)
        _CoreEdgeColor("Core (edges)", Color) = (0.16, 0.08, 0.035, 1)
        _LipColor("Lip (pumpkin flesh)", Color) = (0.78, 0.5, 0.2, 1)
        _WarmColor("Warm tint near the break", Color) = (0.35, 0.12, 0.03, 1)
        _WarmStrength("Warm Strength", Range(0, 1)) = 0.35
        _Shade("Shadow round the cracks", Range(0, 1)) = 0.28
        _Widen("Amount from hairline to full width", Range(0.05, 1)) = 0.45
        _HairWidth("Hairline width (G)", Range(0.01, 0.3)) = 0.1
        _MaxWidth("Full width (G)", Range(0.05, 0.6)) = 0.3
        _LipWidth("Lip width (G)", Range(0, 0.4)) = 0.12
        _HaloWidth("Shadow width (G)", Range(0.05, 1)) = 0.5
        _Collapse("Collapse (draw nothing)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CrackOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CrackAxis;
                half4 _CoreColor;
                half4 _CoreEdgeColor;
                half4 _LipColor;
                half4 _WarmColor;
                half _WarmStrength;
                half _Shade;
                half _CrackAmount;
                half _Widen;
                half _HairWidth;
                half _MaxWidth;
                half _LipWidth;
                half _HaloWidth;
                half _Collapse;
            CBUFFER_END

            TEXTURE2D(_CrackMap);
            SAMPLER(sampler_CrackMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 rest : TEXCOORD3;    // the vertex's rest position, model units (Pumpkin Boss Butt Split)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 rest : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                // Nothing to draw (hidden, or no cracks yet): every vertex outside the view, so no pixel is even visited.
                bool none = _Collapse > 0.5 || _CrackAmount <= 0.0001;
                output.positionCS = none ? float4(2.0, 2.0, 2.0, 1.0) : position.positionCS;
                output.rest = input.rest.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // The cylinder: the angle round the pumpkin (0 at its front) and its height.
                float2 across = input.rest.xz - _CrackAxis.xy;
                float angle = atan2(across.x, across.y);
                float u = angle * (0.5 * INV_PI) + 0.5;
                float v = (input.rest.y - _CrackAxis.z) / max(_CrackAxis.w - _CrackAxis.z, 0.001);
                // The texture's own sharpness from the angle measured from the back, where the front one jumps (no line at
                // the wrap).
                float back = atan2(-across.x, -across.y);
                float2 dA = float2(ddx(angle), ddy(angle));
                float2 dB = float2(ddx(back), ddy(back));
                float2 dAngle = dot(dA, dA) <= dot(dB, dB) ? dA : dB;
                float2 gx = float2(dAngle.x * (0.5 * INV_PI), ddx(v));
                float2 gy = float2(dAngle.y * (0.5 * INV_PI), ddy(v));
                half3 map = SAMPLE_TEXTURE2D_GRAD(_CrackMap, sampler_CrackMap, float2(u, v), gx, gy).rgb;

                half amount = _CrackAmount;
                // Its crack hasn't grown this far yet.
                if (map.r > amount) discard;
                half grown = saturate((amount - map.r) / _Widen);
                half halfWidth = lerp(_HairWidth, _MaxWidth, grown);
                half aa = max(fwidth(map.g), 0.0001);
                half core = 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, map.g);
                // Thinner than a pixel: it fades rather than sparkles.
                core *= saturate(halfWidth / aa);
                half lip = saturate((1.0 - smoothstep(halfWidth + _LipWidth - aa, halfWidth + _LipWidth + aa, map.g)) - core);
                lip *= saturate(map.b * 3.0 - 1.2);
                half halo = (1.0 - saturate((map.g - halfWidth) / _HaloWidth)) * (1.0 - core);

                // The core: deep brown at its edges, near black down its middle; a faint warm tint deep in the widest close
                // to the break.
                half deep = saturate((halfWidth - map.g) / max(halfWidth, 0.001));
                half3 coreColour = lerp(_CoreEdgeColor.rgb, _CoreColor.rgb, deep);
                coreColour += _WarmColor.rgb * (_WarmStrength * deep * grown * saturate((amount - 0.85) / 0.15));

                half3 colour = 0;
                half alpha = halo * _Shade;
                colour = lerp(colour, _LipColor.rgb, lip);
                alpha = lerp(alpha, 1.0, lip);
                colour = lerp(colour, coreColour, core);
                alpha = lerp(alpha, 1.0, core);
                if (alpha <= 0.002) discard;

                // Lit like the pumpkin round it (ambient and the main light, plainly).
                half3 n = normalize(input.normalWS);
                Light sun = GetMainLight();
                colour *= SampleSH(n) + sun.color * saturate(dot(n, sun.direction));
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
