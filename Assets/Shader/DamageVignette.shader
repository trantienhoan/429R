// Red hit lines around the edges of the player's view (Damage Vignette). Drawn on the inside of a sphere around the
// headset camera, so it lines up in both eyes. Thin spikes point in towards the middle of the view, each with its own
// width and length, over a soft red glow at the rim; _Intensity (0-1) fades it, pulling the spikes back out to the edge.
// _HitDir (camera space, w = 1) lights only the arc of the edge on the side the hit came from; w = 0 lights it all.
Shader "429/Damage Vignette"
{
    Properties
    {
        _Color("Color", Color) = (0.85, 0.02, 0.02, 1)
        _Intensity("Intensity", Range(0, 1)) = 0
        _Seed("Seed (a new line pattern per hit)", Float) = 0
        _Lines("Lines around the view", Float) = 64
        _Inner("Closest a line reaches in (degrees off centre)", Range(5, 60)) = 35
        _Reach("Spread of line lengths (degrees)", Range(0, 40)) = 9
        _Taper("How far from its tip a line reaches full width (degrees)", Range(1, 20)) = 6
        _Retreat("How far lines pull back out as they fade (degrees)", Range(0, 30)) = 7
        _Glow("Edge glow", Range(0, 1)) = 0.45
        _GlowFrom("Edge glow starts (degrees off centre)", Range(0, 90)) = 32
        _GlowTo("Edge glow full (degrees off centre)", Range(0, 90)) = 60
        _HitDir("Hit direction (camera space; w 1 = on)", Vector) = (0, 0, 1, 0)
        _Arc("Half-width of the lit arc (degrees)", Range(10, 180)) = 60
        _AllAround("Strength all around when the hit is straight ahead or behind", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _Intensity;
            float _Seed;
            float _Lines;
            float _Inner;
            float _Reach;
            float _Taper;
            float _Retreat;
            float _Glow;
            float _GlowFrom;
            float _GlowTo;
            float4 _HitDir;
            float _Arc;
            float _AllAround;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;   // the sphere sits on the camera: +Z is straight ahead
                return o;
            }

            float hash(float n)
            {
                return frac(sin(n * 12.9898 + _Seed * 78.233) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 d = normalize(i.dir);
                float offCentre = degrees(acos(clamp(d.z, -1.0, 1.0)));
                float around = atan2(d.y, d.x) / 6.2831853 + 0.5;

                // One line per slice of the view, each with its own width, length and chance of being there at all.
                float slice = around * _Lines;
                float id = floor(slice);
                float across = 1.0 - abs(frac(slice) - 0.5) * 2.0;   // 1 down the middle of the slice
                float width = lerp(0.12, 0.5, hash(id + 31.7));
                float tip = _Inner + hash(id) * _Reach + (1.0 - _Intensity) * _Retreat;
                float outward = saturate((offCentre - tip) / _Taper);   // 0 at the tip, growing towards the edge
                float edge = 1.0 - width * outward;                   // the line widens from a sharp point
                float lineMask = smoothstep(edge, edge + 0.08, across) * step(tip, offCentre) * step(0.25, hash(id + 57.1));

                float glow = smoothstep(_GlowFrom, _GlowTo, offCentre) * _Glow;
                // Only the side the hit came from: how close this point of the edge is (around the view) to the hit's
                // direction. A hit from almost straight ahead or behind has no side, so the whole edge glows, weaker.
                float side = 1.0;
                if (_HitDir.w > 0.5)
                {
                    float2 here = d.xy / max(length(d.xy), 1e-4);
                    float hitLength = length(_HitDir.xy);
                    float2 from = _HitDir.xy / max(hitLength, 1e-4);
                    float arc = smoothstep(cos(radians(_Arc)), 1.0, dot(here, from));
                    float noSide = 1.0 - saturate(hitLength / 0.3);
                    side = max(arc, noSide * _AllAround);
                }
                float alpha = saturate(lineMask * 0.95 + glow) * _Intensity * _Color.a * side;
                fixed3 colour = lerp(_Color.rgb * 0.6, _Color.rgb, lineMask);
                return fixed4(colour, alpha);
            }
            ENDCG
        }
    }
}
