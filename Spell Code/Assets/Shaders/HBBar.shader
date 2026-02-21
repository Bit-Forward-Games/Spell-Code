Shader "Custom/PaletteSwapGlowOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PaletteTex ("TargetTexture", 2D) = "white" {}
        _Alpha ("Alpha", Range(0, 1)) = 1.0
        _Brightness ("Brightness", Range(0, 2)) = 1.0
        _AccentColor ("Accent Color", Color) = (1, 0, 0, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 1
        _OutlineThickness ("Outline Thickness", Range(0, 5)) = 1
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed brightness : TEXCOORD1;
                fixed alpha : TEXCOORD2;
            };

            sampler2D _MainTex;
            sampler2D _PaletteTex;
            float4 _MainTex_TexelSize; // Needed for sampling neighboring pixels
            float _Alpha;
            float _Brightness;
            fixed4 _AccentColor;
            float _GlowIntensity;
            float _OutlineThickness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.brightness = saturate(_Brightness);
                o.alpha = saturate(_Alpha);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample main texture color
                fixed4 mainColor = tex2D(_MainTex, i.uv);

                // Use the red channel as the palette index
                fixed x = mainColor.r;

                // Sample the palette texture
                fixed4 paletteColor = tex2D(_PaletteTex, float2(x, 0));

                // Apply brightness and alpha
                paletteColor.a = mainColor.a * i.alpha; 
                paletteColor.rgb *= i.brightness;

                // ---- Outline Detection ----

                // Sample neighboring alpha values
                float alphaUp    = tex2D(_MainTex, i.uv + float2(0, _MainTex_TexelSize.y * _OutlineThickness)).a;
                float alphaDown  = tex2D(_MainTex, i.uv - float2(0, _MainTex_TexelSize.y * _OutlineThickness)).a;
                float alphaLeft  = tex2D(_MainTex, i.uv - float2(_MainTex_TexelSize.x * _OutlineThickness, 0)).a;
                float alphaRight = tex2D(_MainTex, i.uv + float2(_MainTex_TexelSize.x * _OutlineThickness, 0)).a;

                float edgeMask = step(0.01, alphaUp) + step(0.01, alphaDown) + step(0.01, alphaLeft) + step(0.01, alphaRight);
                edgeMask = step(0.01, edgeMask) * step(mainColor.a, 0.01); // Detect edge pixels (inside empty space but near the sprite)

                // ---- Apply Accent Color ONLY to the Outline ----
                fixed4 accentLayer = _AccentColor;
                accentLayer.a *= edgeMask; // Only apply to outline

                // ---- Apply Glow Effect on Outline ----
                float glowFactor = smoothstep(0.0, 0.1, edgeMask);
                accentLayer.rgb *= (1.0 + glowFactor * _GlowIntensity);

                // ---- Final Output ----
                paletteColor = lerp(paletteColor, accentLayer, edgeMask); // Blend only on the outline

                return paletteColor;
            }

            ENDCG
        }
    }
}
