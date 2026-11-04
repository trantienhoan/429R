// The paint in Special Stage Type 2's buckets (Paint Bees): a liquid surface on a fine flat disc (Paint_Disc, radius 1
// in its own space, scaled to the bucket). Cheap: no textures, no reflections, all in the vertex shader but the light.
// - slow waves cross it (two sets at an angle) and a ripple ring spreads from where a hand went in (_Ripple, set every
//   frame by Paint Bucket: x, z = the spot on the disc, z = seconds since, w = how strong), all held still at the rim;
// - its colour (_BaseColor, set by the bucket) swirls slowly, lighter streaks round the middle; darker at the rim;
// - lit by the main light and the ambient, with a sharp highlight and a soft "sky" glint that ride on the waves, a
//   brighter rim at grazing angles (Fresnel) and its glow (_EmissionColor); fades into the fog; single-pass VR.
Shader "429/Paint Liquid"
{
    Properties
    {
        [MainColor] _BaseColor("Colour", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Glow", Color) = (0, 0, 0, 1)
        _WaveHeight("Wave Height (m)", Range(0, 0.02)) = 0.0025
        _WaveLength("Wave Length (m)", Range(0.02, 0.5)) = 0.11
        _WaveSpeed("Wave Speed", Range(0, 5)) = 1.3
        _RippleHeight("Ripple Height (m)", Range(0, 0.03)) = 0.006
        _RippleLength("Ripple Length (m)", Range(0.01, 0.2)) = 0.05
        _RippleSpeed("Ripple Speed (m/s)", Range(0.05, 2)) = 0.32
        _RippleFade("Ripple Fade (per second)", Range(0.1, 10)) = 2.2
        _Swirl("Swirl", Range(0, 1)) = 0.35
        _SwirlSpeed("Swirl Speed", Range(0, 3)) = 0.5
        _RimDark("Darker At The Rim", Range(0, 1)) = 0.35
        _Shine("Highlight", Range(0, 3)) = 1.2
        _Glint("Sky Glint", Range(0, 2)) = 0.9
        _Fresnel("Edge Shine", Range(0, 1)) = 0.3
        _SelfLit("Own Light (reads in dark rooms)", Range(0, 1)) = 0.4
        _CrestLight("Lighter Crests", Range(0, 1)) = 0.35
        _Ripple("Ripple (x, z on the disc, age s, strength)", Vector) = (0, 0, 100, 0)
    }

    HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                float _RippleHeight;
                float _RippleLength;
                float _RippleSpeed;
                float _RippleFade;
                half _Swirl;
                half _SwirlSpeed;
                half _RimDark;
                half _Shine;
                half _Glint;
                half _Fresnel;
                half _SelfLit;
                half _CrestLight;
                float4 _Ripple;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // How high the surface is at p (metres on the disc; 'size' = the disc's radius in metres).
            float Height(float2 p, float size, float t)
            {
                float k = 6.2831853 / _WaveLength;
                float waves = sin(dot(p, float2(0.8, 0.6)) * k + t * _WaveSpeed) * 0.6
                            + sin(dot(p, float2(-0.35, 0.94)) * k * 1.37 - t * _WaveSpeed * 1.21) * 0.4;
                float h = waves * _WaveHeight;
                // The ripple: a ring of little waves running out from where the hand went in, dying away.
                float d = distance(p, _Ripple.xy * size);
                float front = _Ripple.z * _RippleSpeed;
                float packet = saturate(1.0 - abs(d - front) / (_RippleLength * 1.5));
                h += _Ripple.w * _RippleHeight * exp(-_Ripple.z * _RippleFade) * packet
                   * sin((d - front) * 6.2831853 / _RippleLength);
                // Held still at the bucket's wall.
                float out01 = length(p) / size;
                return h * smoothstep(1.0, 0.82, out01);
            }

            // The surface at this vertex: displaced position (object space) and normal (metre-scaled object space).
            void Displace(float3 positionOS, out float3 pos, out float3 normalM, out float size, out float2 p, out float crest)
            {
                size = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                p = positionOS.xz * size;
                float t = _Time.y;
                float h = Height(p, size, t);
                const float e = 0.004;
                float dx = Height(p + float2(e, 0), size, t) - Height(p - float2(e, 0), size, t);
                float dz = Height(p + float2(0, e), size, t) - Height(p - float2(0, e), size, t);
                normalM = normalize(float3(-dx / (2 * e), 1, -dz / (2 * e)));
                pos = positionOS + float3(0, h, 0);
                // -1 in a trough .. 1 on a crest (of the waves or the ripple).
                crest = clamp(h / max(1e-5, max(_WaveHeight, _RippleHeight * _Ripple.w * 0.6)), -1, 1);
            }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PaintLiquid"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 disc : TEXCOORD2;     // x, z on the disc (metres from the middle), how far out (0 middle, 1 rim)
                half fogFactor : TEXCOORD3;
                half crest : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 pos, normalOS;
                float size;
                float2 p;
                float crest;
                Displace(input.positionOS.xyz, pos, normalOS, size, p, crest);
                output.crest = crest;
                VertexPositionInputs position = GetVertexPositionInputs(pos);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                // The disc is scaled flat (x, z): its normal in the world from the scaled frame.
                output.normalWS = normalize(TransformObjectToWorldDir(float3(normalOS.x / size, normalOS.y, normalOS.z / size)));
                output.disc = float3(p, length(p) / size);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = normalize(input.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float out01 = input.disc.z;

                // The colour, swirling slowly (lighter streaks), darker at the rim.
                float angle = atan2(input.disc.y, input.disc.x);
                half swirl = sin(angle * 2.0 + out01 * 9.0 - _Time.y * _SwirlSpeed) * 0.5 + 0.5;
                half3 colour = _BaseColor.rgb;
                colour = lerp(colour, colour * 1.25 + 0.06, swirl * swirl * _Swirl);
                colour *= lerp(1.0, 1.0 - _RimDark, smoothstep(0.7, 1.0, out01));
                colour *= 1.0 + input.crest * _CrestLight;

                Light sun = GetMainLight();
                half3 lit = colour * (SampleSH(n) + sun.color * saturate(dot(n, sun.direction)) * 0.8 + _SelfLit);
                half3 hv = normalize(sun.direction + v);
                lit += sun.color * pow(saturate(dot(n, hv)), 140.0) * _Shine;
                half3 r = reflect(-v, n);
                half sky = saturate(dot(r, normalize(half3(0.3, 1.0, 0.25))));
                lit += pow(sky, 60.0) * _Glint + pow(sky, 6.0) * _Glint * 0.12 * (colour + 0.3);
                lit += pow(1.0 - saturate(dot(n, v)), 4.0) * _Fresnel * (colour * 0.5 + 0.5);
                lit += _EmissionColor.rgb;
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1.0);
            }
            ENDHLSL
        }

        // The same surface into the depth prepass (when the pipeline draws one).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
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
                float3 pos, normalM;
                float size;
                float2 p;
                float crest;
                Displace(input.positionOS.xyz, pos, normalM, size, p, crest);
                output.positionCS = TransformObjectToHClip(pos);
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
    FallBack Off
}
