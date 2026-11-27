Shader "Custom/CupWithFill"
{
    Properties
    {
        // Surface Shader 标准属性声明，[SurfaceType] 和 [BlendMode] 让 Unity 自动处理
        [MainTexture] _MainTex ("Albedo", 2D) = "white" {}
        [MainColor] _Color ("Color", Color) = (1,1,1,1)
        _FillY ("Fill Y (model space)", Float) = -1000
        // Built-in RP Surface Shader 自动生成这些，但声明一下让 Inspector 显示
        [HideInInspector] _Surface ("__surface", Float) = 0.0
        [HideInInspector] _Mode ("__mode", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderQueue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow exclude_path:prepass

        sampler2D _MainTex;
        fixed4 _Color;
        float _FillY;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;     // 用于转回模型空间和 _FillY 比较
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // worldPos → 模型空间 pos.y
            float3 modelPos = mul(unity_WorldToObject, float4(IN.worldPos, 1)).xyz;

            // 裁剪液面以下的杯壁像素 → 露出后面的液体 Mesh
            if (modelPos.y < _FillY)
                discard;

            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Metallic = 0;
            o.Smoothness = 0.5;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
