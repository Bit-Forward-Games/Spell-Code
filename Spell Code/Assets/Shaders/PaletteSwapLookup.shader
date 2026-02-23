Shader "Custom/PaletteSwapLookup"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PaletteTex("TargetTexture", 2D) = "white" {}
        _Alpha ("Alpha", Range(0, 1)) = 1.0
        _Brightness ("Brightness", Range(0, 2)) = 1.0
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
                float4 vertex : POSITION;   // Vertex position input
                float2 uv : TEXCOORD0;     // Texture coordinates input
            };

            struct v2f
            {
                float4 vertex : SV_POSITION; // Vertex position output to clip space
                float2 uv : TEXCOORD0;       // Texture coordinates output to fragment shader
                fixed brightness : TEXCOORD1; // Brightness passed to fragment shader (new varying)
                fixed alpha : TEXCOORD2;      // Alpha passed to fragment shader (new varying)
            };

            sampler2D _MainTex;
            sampler2D _PaletteTex;
            float _Alpha;
            float _Brightness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex); // Transform vertex to clip space
                o.uv = v.uv;

                // Pass precomputed brightness and alpha values to the fragment shader
                o.brightness = saturate(_Brightness); // Clamp brightness between 0 and 2 for safety
                o.alpha = saturate(_Alpha);           // Clamp alpha between 0 and 1 for safety

                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the main texture color at the given UV coordinates
                fixed4 mainColor = tex2D(_MainTex, i.uv);

                // Use the red channel of the main texture to index into the palette texture
                fixed x = mainColor.r;

                // Sample the palette texture using the calculated index value
                fixed4 paletteColor = tex2D(_PaletteTex, float2(x, 0));

                // Apply precomputed alpha and brightness from the vertex shader
                paletteColor.a = mainColor.a * i.alpha; 
                paletteColor.rgb *= i.brightness;

                return paletteColor;
            }

            ENDCG
        }
    }
}