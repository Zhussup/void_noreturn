// Постэффект CRT на камере: скан-линии, мерцание кинескопа, расфокус сходимости
// RGB, прокат кадровой развёртки и «пересканирование» — дёрганье полос, как будто
// нас вычисляют. Включается короткими всплесками: только когда кибербезы
// проверяют логи (NightDirector.FlashCrt из TickCyberbez), и тут же гаснет.
// Built-in RP, вешается на камеру скриптом CrtFilter.
Shader "VOID/CRT_Post"
{
    Properties
    {
        _MainTex ("Кадр", 2D) = "white" {}
        [Header(Code hooks)]
        _CrtIntensity ("Сила CRT", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _CrtIntensity;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float k = saturate(_CrtIntensity);
                float t = _Time.y;

                // ---- джиттер полос: кадр будто пересканируют
                float row = floor(i.uv.y * 96.0);
                float step12 = floor(t * 12.0);
                float band = step(1.0 - 0.012 - 0.05 * k, Hash(float2(row, step12 + 31.0)));
                float2 uv = i.uv + float2(band * (Hash(float2(row + 7.7, step12)) - 0.5) * 0.008, 0);

                // ---- расфокус сходимости: R/B чуть разъезжаются
                float ca = _MainTex_TexelSize.x * (0.7 + 0.8 * k);
                float3 col;
                col.r = tex2D(_MainTex, uv + float2(ca, 0)).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - float2(ca, 0)).b;

                // ---- скан-линии; на мелких плотностях гаснут, без муара
                float sCount = _ScreenParams.y * 0.5;
                float s = 0.5 + 0.5 * cos(i.uv.y * sCount * 6.2831853);
                float density = fwidth(i.uv.y) * sCount;
                float scanFade = saturate(1.5 - density * 2.0);
                col *= 1.0 - (0.14 + 0.14 * k) * (1.0 - s) * scanFade;

                // ---- кадровая развёртка: тусклая полоса прокатывается вниз
                float roll = exp(-abs(frac(t * 0.07) - uv.y) * 18.0);
                col *= 1.0 - 0.10 * k * roll;

                // ---- «сканируют»: яркая строка прочёсывает кадр сверху вниз
                col += exp(-abs(frac(t * 0.23) - uv.y) * 90.0) * 0.20 * k;

                // ---- мерцание яркости, как у кинескопа
                float flick = 0.5 + 0.5 * sin(t * 59.0);
                col *= 1.0 - (0.02 + 0.05 * k) * flick
                          - (0.015 + 0.03 * k) * Hash(float2(floor(t * 20.0), 9.0));

                // ---- снег на высоких значениях
                col += (Hash(i.uv + float2(t * 17.3, 0.0)) - 0.5) * (0.02 + 0.06 * k);

                // ---- холодная surveillance-хроматика
                col = lerp(col, col * float3(0.94, 1.0, 0.95), 0.18 * k);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}