// See-through stand-in for the 3D wires that are not selected, so a clicked wire can be followed through the ducts.
Shader "A11/Ghost"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.12)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        ZTest LEqual
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 n : TEXCOORD1;
                float3 v : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.v = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Facing the camera: a little body; at the silhouette: a little more, so the tube still reads as a tube.
                float f = saturate(dot(normalize(i.n), normalize(i.v)));
                float3 c = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                return float4(c * (0.55 + 0.45 * f), _Color.a * (1.6 - f));
            }
            ENDCG
        }
    }
}
