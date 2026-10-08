// Two-colour liquid inside a circle. Team A fills from the bottom up to _Level;
// the surface waves with amplitude _Amp. Draw it on a UI Image with no sprite.
Shader "UI/TugLiquid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorA ("Team A", Color) = (0.361, 0.949, 0.769, 1)
        _ColorB ("Team B", Color) = (1, 0.71, 0.278, 1)
        _Level ("Surface height 0-1", Range(0,1)) = 0.5
        _Amp ("Wave amplitude", Range(0,0.2)) = 0.02
        _Phase ("Wave phase", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            fixed4 _Color, _ColorA, _ColorB;
            float _Level, _Amp, _Phase;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;                 // -1..1 across the circle
                float r = length(p);
                float aa = fwidth(r) * 1.5;
                float inside = 1 - smoothstep(1 - aa, 1, r);

                // Wavy surface. Waves fade near the walls so the liquid meets the glass cleanly.
                float wave = sin(i.uv.x * 7.0 + _Phase * 2.3) * 0.6 + sin(i.uv.x * 13.0 - _Phase * 3.7) * 0.4;
                float level = _Level + _Amp * wave * (1 - p.x * p.x * 0.6);
                float d = level - i.uv.y;
                float ew = fwidth(i.uv.y) * 1.5;
                float teamA = smoothstep(-ew, ew, d);

                float3 col = lerp(_ColorB.rgb, _ColorA.rgb, teamA);
                col *= lerp(1.0, 0.62, smoothstep(0.45, 1.0, r));        // darker toward the glass
                col *= lerp(0.88, 1.08, i.uv.y);                         // light from above
                col += exp(-abs(d) * 120.0) * 0.45;                      // bright line on the surface
                float2 s = p - float2(-0.38, 0.46);
                col += exp(-dot(s, s) * 9.0) * 0.22;                     // glass highlight
                col += smoothstep(0.92, 1.0, r) * 0.18;                  // rim

                return fixed4(saturate(col), inside) * i.color;
            }
            ENDCG
        }
    }
}
