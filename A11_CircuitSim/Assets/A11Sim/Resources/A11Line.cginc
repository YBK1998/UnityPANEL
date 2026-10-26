// Shared code for the A11 line shaders.
// Each segment is a quad. The vertex shader expands it to the line width (never thinner than
// _MinPx screen pixels) so lines stay visible at every zoom level. The fragment shader computes
// the distance to the segment, which gives round caps and round joins.
#include "UnityCG.cginc"

float4 _Color;
float _Intensity;
float _WidthMul;
float _MinPx;
float _FlowAmt;
float _FlowSpeed;
float _FlowFreq;
float _Flicker;
float _MaxW;
float _WorldPerPixel; // global, set by CameraRig

struct appdata
{
    float4 vertex : POSITION;
    float4 uv0 : TEXCOORD0; // arc length, base width, side (-1/+1), end sign (-1 start / +1 end)
    float4 uv1 : TEXCOORD1; // direction x, direction y, segment length
    float4 color : COLOR;
};

struct v2f
{
    float4 pos : SV_POSITION;
    float4 col : COLOR;
    float4 d : TEXCOORD0; // along, across, segment length, half width
    float arc : TEXCOORD1;
};

v2f vert(appdata v)
{
    v2f o;
    float wpp = max(_WorldPerPixel, 1e-5);
    float hw = max(min(v.uv0.y * _WidthMul, _MaxW), _MinPx * wpp) * 0.5;
    float side = v.uv0.z;
    float endSign = v.uv0.w;
    float2 dir = v.uv1.xy;
    float2 perp = float2(-dir.y, dir.x);
    float segLen = v.uv1.z;
    float3 p = v.vertex.xyz;
    p.xy += perp * side * hw + dir * endSign * hw;
    o.pos = UnityObjectToClipPos(float4(p, 1));
    o.col = v.color;
    o.d = float4(endSign < 0 ? -hw : segLen + hw, side * hw, segLen, hw);
    o.arc = v.uv0.x + endSign * hw;
    return o;
}

float FlowMod(float arc)
{
    float w = 0.5 + 0.5 * sin((arc * _FlowFreq - _Time.y * _FlowSpeed) * 6.2831853);
    w = w * w * w;
    float flick = lerp(1.0, 0.25 + 0.75 * step(0.45, frac(_Time.y * 13.0 + arc * 0.35)), _Flicker);
    return lerp(1.0, 0.55 + 1.1 * w, _FlowAmt) * flick;
}

float SegDist(v2f i)
{
    float a = i.d.x;
    float L = i.d.z;
    float da = max(0, max(-a, a - L));
    return length(float2(da, i.d.y));
}
