// Solid anti-aliased line core (premultiplied alpha).
Shader "A11/LineCore"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _WidthMul ("Width multiplier", Float) = 1
        _MinPx ("Minimum width (px)", Float) = 1.4
        _FlowAmt ("Flow amount", Float) = 0
        _FlowSpeed ("Flow speed", Float) = 0
        _FlowFreq ("Flow frequency", Float) = 0.35
        _Flicker ("Flicker", Float) = 0
        _MaxW ("Max base width", Float) = 1000
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "A11Line.cginc"

            fixed4 frag(v2f i) : SV_Target
            {
                float dist = SegDist(i);
                float hw = i.d.w;
                float wpp = max(_WorldPerPixel, 1e-5);
                float cov = saturate((hw - dist) / wpp + 0.5);
                float4 c = _Color * i.col;
                float a = cov * c.a;
                float3 rgb = c.rgb * _Intensity * FlowMod(i.arc);
                return float4(rgb * a, a);
            }
            ENDCG
        }
    }
}
