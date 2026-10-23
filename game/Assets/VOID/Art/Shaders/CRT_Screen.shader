// Экран ЭЛТ-монитора: показывает RenderTexture с текстом (_MainTex, её подаёт
// ScreenSurface через MaterialPropertyBlock) и добавляет тонкие эффекты кинескопа.
// Built-in RP, unlit — экран сам светится и не зависит от света комнаты.
// По умолчанию всё слабое: главное — читаемость текста с ~0.9 м при FOV 45.
Shader "VOID/CRT_Screen"
{
    Properties
    {
        _MainTex ("Экран (RenderTexture)", 2D) = "black" {}
        _Brightness ("Яркость", Range(0.5, 3)) = 1.3
        _Scanlines ("Строки развёртки", Range(0, 1)) = 0.18
        _ScanlineCount ("Число строк развёртки", Float) = 320
        _Curvature ("Изгиб кинескопа", Range(0, 0.4)) = 0.06
        _Glow ("Свечение букв", Range(0, 2)) = 0.7
        _GlowRadius ("Радиус свечения, тексели", Range(0.5, 8)) = 2.5
        _Vignette ("Виньетка", Range(0, 1)) = 0.35
        _Flicker ("Мерцание", Range(0, 0.2)) = 0.015
        _ChromaShift ("Расслоение цветов, тексели", Range(0, 4)) = 0.6
        [Header(Code hooks)]
        _Darken ("Затемнение (тревога)", Range(0, 1)) = 0
        _Glitch ("Глитч (VOID)", Range(0, 1)) = 0
        [HideInInspector] _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Brightness, _Scanlines, _ScanlineCount, _Curvature;
            float _Glow, _GlowRadius, _Vignette, _Flicker, _ChromaShift;
            float _Darken, _Glitch, _Seed;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // каналы со сдвигом по X — лёгкое расслоение цветов
            float3 SampleSplit(float2 uv, float shift)
            {
                float r = tex2D(_MainTex, uv + float2(shift, 0)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2(shift, 0)).b;
                return float3(r, g, b);
            }

            // дешёвое свечение: 8 отсчётов по кольцу и 4 по диагоналям дальше
            float3 Bloom(float2 uv)
            {
                float2 r = _GlowRadius * _MainTex_TexelSize.xy;
                float3 s = 0;
                s += tex2D(_MainTex, uv + float2( r.x, 0)).rgb;
                s += tex2D(_MainTex, uv + float2(-r.x, 0)).rgb;
                s += tex2D(_MainTex, uv + float2(0,  r.y)).rgb;
                s += tex2D(_MainTex, uv + float2(0, -r.y)).rgb;
                s += tex2D(_MainTex, uv + r * float2( 0.7,  0.7)).rgb;
                s += tex2D(_MainTex, uv + r * float2(-0.7,  0.7)).rgb;
                s += tex2D(_MainTex, uv + r * float2( 0.7, -0.7)).rgb;
                s += tex2D(_MainTex, uv + r * float2(-0.7, -0.7)).rgb;
                s += tex2D(_MainTex, uv + r * float2( 1.6,  1.6)).rgb * 0.5;
                s += tex2D(_MainTex, uv + r * float2(-1.6,  1.6)).rgb * 0.5;
                s += tex2D(_MainTex, uv + r * float2( 1.6, -1.6)).rgb * 0.5;
                s += tex2D(_MainTex, uv + r * float2(-1.6, -1.6)).rgb * 0.5;
                return s / 10.0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;

                // ---- бочкообразный изгиб: края уходят за пределы, там чёрное
                float2 c = i.uv * 2.0 - 1.0;
                c *= 1.0 + _Curvature * dot(c, c) * 0.5;
                float2 uv = c * 0.5 + 0.5;
                float2 fw = max(fwidth(uv) * 1.5, 1e-4);
                float2 edge = smoothstep(0, fw, uv) * smoothstep(0, fw, 1.0 - uv);
                float inside = edge.x * edge.y;

                // ---- глитч: горизонтальные полосы со сдвигом, дёрганье по Y
                float step14 = floor(t * 14.0);
                float band = floor(uv.y * 48.0);
                float pick = Hash(float2(band, step14 + _Seed));
                float active = step(1.0 - _Glitch * 0.7, pick) * step(0.001, _Glitch);
                uv.x += active * (Hash(float2(band + 3.1, step14)) - 0.5) * 0.12 * _Glitch;
                uv.y += _Glitch * 0.012 * (Hash(float2(floor(t * 9.0), _Seed)) - 0.5);

                // ---- цвет: расслоение каналов (при глитче сильнее) + свечение
                float shift = (_ChromaShift + _Glitch * 6.0) * _MainTex_TexelSize.x;
                float3 col = SampleSplit(uv, shift);
                col += Bloom(uv) * _Glow;

                // ---- строки развёртки; гаснут, когда мельче пикселя (без муара)
                float s = 0.5 + 0.5 * cos(uv.y * _ScanlineCount * 6.2831853);
                float density = fwidth(uv.y) * _ScanlineCount;
                float scanFade = saturate(1.5 - density * 2.0);
                col *= 1.0 - _Scanlines * (1.0 - s) * scanFade;

                // ---- виньетка
                float vig = smoothstep(1.45, 0.35, length(c));
                col *= lerp(1.0, vig, _Vignette);

                // ---- мерцание яркости
                float flick = 0.5 + 0.5 * sin(t * 59.0 + _Seed);
                col *= 1.0 - _Flicker * flick - _Flicker * 0.5 * Hash(float2(floor(t * 20.0), _Seed));

                // ---- шум на сорванных полосах
                col += _Glitch * active * 0.12 * Hash(float2(uv.y * 300.0, t));

                col *= _Brightness;
                col *= 1.0 - _Darken * 0.85;
                col *= inside;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
