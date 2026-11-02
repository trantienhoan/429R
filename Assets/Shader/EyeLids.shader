// The player's eyelids in the opening (Opening Cutscene). Drawn on the inside of a sphere that follows the headset, worked
// out from each eye's own view direction, so the edge of the lids lines up in both eyes. _Open: 0 = shut, black all
// over; 1 = wide open, nothing drawn. _Fade makes the whole thing see-through.
Shader "429/Eye Lids"
{
    Properties
    {
        _Open("How open", Range(0, 1)) = 0
        _Feather("Softness of the lids' edge", Range(0.01, 1)) = 0.28
        _Fade("How solid", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Overlay+2" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
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

            // How far up and down the view reaches when wide open (the tangent of the angle), past any headset's view.
            static const float WideOpen = 2.4;
            // The lids curve down towards the sides.
            static const float Curve = 0.25;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 look : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _Open;
            float _Feather;
            float _Fade;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                // From this eye, in the headset's own axes.
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.look = mul((float3x3)unity_WorldToObject, world - _WorldSpaceCameraPos.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 look = normalize(i.look);
                float ahead = max(look.z, 0.001);
                float up = abs(look.y) / ahead;
                float side = look.x / ahead;
                float edge = _Open * WideOpen * saturate(1.0 - Curve * side * side);
                // Shut (_Open 0): the edge goes past the far side of the softness, so it's black everywhere.
                float shut = smoothstep(edge - _Feather * _Open, edge + _Feather * _Open + 0.0001, up);
                if (_Open <= 0.001) shut = 1.0;
                return fixed4(0, 0, 0, shut * _Fade);
            }
            ENDCG
        }
    }
}
