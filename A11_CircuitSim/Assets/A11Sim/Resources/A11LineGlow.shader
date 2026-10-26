// Soft halo around energized lines. BlendOp Max: overlapping halos (corners, parallel wires)
// do not add up into hot spots. No post-processing or bloom is necessary.
Shader "A11/LineGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _WidthMul ("Width multiplier", Float) = 8
        _MinPx ("Minimum width (px)", Float) = 12
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
        BlendOp Max
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "A11Line.cginc"

            fixed4 frag(v2f i) : SV_Target
            {
                float t = SegDist(i) / i.d.w;
                float g = exp(-t * t * 3.2) * saturate(1.0 - t);
                float3 rgb = _Color.rgb * i.col.rgb * i.col.a * _Intensity * g * FlowMod(i.arc);
                return float4(rgb, g);
            }
            ENDCG
        }
    }
}
