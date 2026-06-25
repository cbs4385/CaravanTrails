Shader "Custom/WarmGradientSky"
{
    Properties
    {
        _TopColor    ("Top Color",     Color) = (0.38, 0.48, 0.70, 1)
        _HorizonColor("Horizon Color", Color) = (0.88, 0.62, 0.28, 1)
        _BottomColor ("Bottom Color",  Color) = (0.58, 0.43, 0.26, 1)
        _HorizonExp  ("Horizon Curve", Float) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor;
            float  _HorizonExp;

            struct v2f { float3 dir : TEXCOORD0; float4 pos : SV_POSITION; };

            v2f vert(float4 v : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v);
                o.dir = v.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float h = normalize(i.dir).y;   // -1 (nadir) → +1 (zenith)
                fixed4 col;
                if (h >= 0.0)
                    col = lerp(_HorizonColor, _TopColor,   pow(h, _HorizonExp));
                else
                    col = lerp(_HorizonColor, _BottomColor, pow(-h, _HorizonExp));
                return col;
            }
            ENDCG
        }
    }
}
