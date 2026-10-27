// The dark of the picture puzzle's "space" (Picture Puzzle). Drawn on the inside of a sphere round the headset camera,
// so it lines up in both eyes: it darkens the room behind to _Color (its alpha = how dark), and it clears the depth (it
// writes the far plane), so what's drawn after it (render queue 4002 and up: the picture, its pieces, the hands, the
// stars) is never hidden by a wall or furniture, and still sorts among itself.
Shader "429/Space Backdrop"
{
    Properties
    {
        _Color("Colour (alpha = how dark)", Color) = (0.01, 0.0, 0.03, 0.92)
    }
    SubShader
    {
        Tags { "Queue" = "Overlay+1" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite On
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i, out float depth : SV_Depth) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // The far plane: everything drawn after this passes the depth test against the room.
            #if defined(UNITY_REVERSED_Z)
                depth = 0;
            #else
                depth = 1;
            #endif
                return _Color;
            }
            ENDCG
        }
    }
}
