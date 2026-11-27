Shader "Custom/MilkTeaLiquid"
{
    Properties
    {
        _Color ("Liquid Color", Color) = (0.65, 0.45, 0.25, 1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.3
        [Toggle] _ForceOpaque ("强制不透明（牛奶/椰果等）", Float) = 0
    }
    SubShader
    {
        // 默认透明队列（茶/咖啡等半透明液体）；强制不透明时渲染逻辑不变（alpha=1 数学上完全挡住背景）
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        fixed4 _Color;
        half _Glossiness;
        float _ForceOpaque;

        struct Input { float2 dummy; };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            // 强制不透明时忽略 _Color.a，直接输出 alpha=1.0
            o.Alpha = _ForceOpaque > 0.5 ? 1.0 : _Color.a;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
}
