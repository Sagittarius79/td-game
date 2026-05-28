Shader "Custom/CloudOverlay"
{
    Properties
    {
        _CloudTex    ("Cloud Texture (alpha channel)", 2D) = "clear" {}
        _CloudScale  ("Scale (kisebb = nagyobb felhők)", Float) = 0.05
        _CloudScroll ("Scroll Speed (xy)", Vector) = (0.5, 0, 0, 0)
        _Alpha       ("Fade Alpha", Float) = 0
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

            sampler2D _CloudTex;
            float _CloudScale;
            float4 _CloudScroll;
            float _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv   = i.worldPos * _CloudScale + _CloudScroll.xy * _Time.y;
                fixed4 cloud = tex2D(_CloudTex, uv);

                // Az alpha channel dönti el, hol takar a felhő.
                // _Alpha = fade-in/out szorzó (0..1)
                return fixed4(cloud.rgb, cloud.a * _Alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
