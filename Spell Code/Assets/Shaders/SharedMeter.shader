Shader "Custom/SharedMeter"
{
    Properties
    {
        _MainTex       ("Texture",           2D   ) = "white" {}
        _GlowIntensity ("Glow Intensity",    Range(0,5)) = 1.0
        _ColorSpeed    ("Color Cycle Speed", Float     ) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4   _MainTex_TexelSize;
            float    _GlowIntensity;
            float    _ColorSpeed;

            // HSV ? RGB helper
            float3 HSVtoRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample base texture
                fixed4 mainColor = tex2D(_MainTex, i.uv);

                // Outline detection (alpha neighbors)
                float a = mainColor.a;
                float alphaUp    = tex2D(_MainTex, i.uv + float2(0,                    _MainTex_TexelSize.y)).a;
                float alphaDown  = tex2D(_MainTex, i.uv - float2(0,                    _MainTex_TexelSize.y)).a;
                float alphaLeft  = tex2D(_MainTex, i.uv - float2(_MainTex_TexelSize.x, 0                  )).a;
                float alphaRight = tex2D(_MainTex, i.uv + float2(_MainTex_TexelSize.x, 0                  )).a;
                float edgeMask   = step(0.01, alphaUp) + step(0.01, alphaDown)
                                 + step(0.01, alphaLeft) + step(0.01, alphaRight);
                edgeMask = step(0.01, edgeMask) * step(a, 0.01);

                // Compute dynamic rainbow color
                float hue = frac(_Time.y * _ColorSpeed);
                float3 rgb = HSVtoRGB(float3(hue, 1.0, 1.0));

                // Build accent layer with glow
                fixed4 accentLayer;
                accentLayer.rgb = rgb * _GlowIntensity;
                accentLayer.a   = edgeMask;

                // Blend outline accent over the main
                return lerp(mainColor, accentLayer, edgeMask);
            }
            ENDCG
        }
    }
}
