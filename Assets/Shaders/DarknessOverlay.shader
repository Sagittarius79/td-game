Shader "Custom/DarknessOverlay"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
        _Alpha ("Alpha", Float) = 0
        _SoftEdge ("Soft Edge", Range(0,1)) = 0.4

        _CloudTex ("Cloud Texture", 2D) = "black" {}
        _CloudScale ("Cloud Scale (smaller = bigger clouds)", Float) = 0.05
        _CloudScroll ("Cloud Scroll Speed (xy)", Vector) = (0.5, 0, 0, 0)
        _CloudIntensity ("Cloud Intensity", Range(0,1)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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

            #define MAX_LIGHTS 64

            fixed4 _Color;
            float _Alpha;
            float _SoftEdge;
            int _LightCount;
            // xy = world pos, z = radius
            float4 _Lights[MAX_LIGHTS];

            sampler2D _CloudTex;
            float _CloudScale;
            float4 _CloudScroll;
            float _CloudIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1 = fully dark, 0 = fully clear (inside a tower)
                float darkness = 1.0;

                for (int idx = 0; idx < _LightCount; idx++)
                {
                    float2 lightPos = _Lights[idx].xy;
                    float radius   = _Lights[idx].z;
                    float dist     = distance(i.worldPos, lightPos);

                    float innerRadius = radius * (1.0 - _SoftEdge);
                    float factor = smoothstep(innerRadius, radius, dist);

                    darkness = min(darkness, factor);
                }

                // Cloud texture — world-space tiled, scrolled by time
                float2 cloudUV = i.worldPos * _CloudScale + _CloudScroll.xy * _Time.y;
                fixed4 cloud = tex2D(_CloudTex, cloudUV);

                // Luminance — fehér felhő = nagy érték, kék ég = kisebb
                float cloudLum = max(cloud.r, max(cloud.g, cloud.b));

                // Felhők csak a sötét részeken láthatók, és csak ahol nem fény van
                float cloudWeight = cloudLum * darkness * _CloudIntensity;

                // Sötét alap → felhő színe felé keverve
                fixed3 col = lerp(_Color.rgb, cloud.rgb, cloudWeight);
                float a = _Alpha * darkness;

                return fixed4(col, a);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
