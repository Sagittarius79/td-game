Shader "Custom/RainOverlay"
{
    Properties
    {
        _Color ("Tint Color", Color) = (0.3, 0.4, 0.6, 1)
        _Alpha ("Fade Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Alpha;

            struct appdata { float4 vertex : POSITION; };
            struct v2f     { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, _Alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
