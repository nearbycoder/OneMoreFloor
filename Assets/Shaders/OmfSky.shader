// Three-stop gradient skybox with a soft sun glow. No fog, so the backdrop keeps its colours
// while the distant skyline fades into it.
Shader "OMF/SkyGradient"
{
    Properties
    {
        _Top ("Top", Color) = (0.29, 0.48, 0.78, 1)
        _Horizon ("Horizon", Color) = (0.75, 0.89, 0.95, 1)
        _Bottom ("Bottom", Color) = (0.91, 0.85, 0.72, 1)
        _SunColor ("Sun", Color) = (1, 0.9, 0.7, 1)
        _SunDir ("Sun Direction", Vector) = (-0.4, 0.3, 0.8, 0)
        _SunSize ("Sun Glow", Float) = 6
        _HorizonY ("Horizon Height", Float) = 0.02
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Top, _Horizon, _Bottom, _SunColor, _SunDir;
            float _SunSize, _HorizonY;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y - _HorizonY;
                float3 c = y > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(y * 4.0), 0.85))
                                 : lerp(_Horizon.rgb, _Bottom.rgb, saturate(-y * 6));
                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                c += _SunColor.rgb * (pow(s, _SunSize * 8) * 0.9 + pow(s, _SunSize) * 0.25);
                return float4(c, 1);
            }
            ENDHLSL
        }
    }
}
