// The purple light running round the rooftop lid (user, 2026-10-28: "run a purple light around the lid"). Drawn on a
// thin ring mesh (Tools > 429 Game > VFX > Add Running Light Round Lid): UV x = how far round the ring (0-1), UV y = how
// far from the lid's edge (0 at the edge, 1 where it has faded out), vertex alpha = strength of that strip. A faint glow
// all the way round; optionally (Running Head > 0) a bright head with a fading tail going round (its place, _Phase, is
// set every frame by RunningGlow). The lid uses the steady glow only (user, 2026-10-28: "we do not need it to run, just
// shines alone is okay"). Additive, unlit (glows in the dark), fades into the fog like everything else; single-pass VR.
Shader "429/Effects/Running Glow Ring"
{
    Properties
    {
        [HDR] _Color("Colour", Color) = (1.6, 0.55, 2.5, 1)
        _BaseGlow("Glow All Round", Range(0, 1)) = 0.18
        _Comet("Running Head (0 = steady glow only)", Range(0, 1)) = 1
        _Tail("Tail Length (share of the ring)", Range(0.02, 1)) = 0.3
        _HeadBoost("Head Brightness", Range(0, 4)) = 1.5
        _Heads("Heads", Range(1, 4)) = 1
        _Falloff("Fade Away From The Edge", Range(0.5, 6)) = 2.2
        _Phase("Head Position (set by RunningGlow)", Range(0, 1)) = 0
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
            Name "RunningGlow"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _BaseGlow;
                half _Comet;
                half _Tail;
                half _HeadBoost;
                half _Heads;
                half _Falloff;
                float _Phase;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half strength : TEXCOORD1;
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
                output.uv = input.uv;
                output.strength = input.color.a;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // How far behind the head this point is (0 = at the head, growing along the tail).
                float behind = frac((_Phase - input.uv.x) * round(_Heads));
                half t = saturate(1.0h - behind / _Tail);
                half comet = t * t * t + _HeadBoost * pow(t, 40.0h);
                // A short soft front so the head doesn't end in a hard cut.
                comet += saturate(1.0h - (1.0h - behind) / 0.008h) * 0.6h;
                half fade = pow(saturate(1.0h - input.uv.y), _Falloff);
                half3 colour = _Color.rgb * (_BaseGlow + comet * _Comet) * fade * input.strength;
                colour = MixFogColor(colour, half3(0, 0, 0), input.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
