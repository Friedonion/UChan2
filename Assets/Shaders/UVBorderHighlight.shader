Shader "Custom/UVBorderHighlight"
{
    Properties
    {
        [HDR] _BaseColor("Color", Color) = (2.2, 1.4, 0.3, 1)
        _Thickness("Border Thickness", Range(0.0, 0.5)) = 0.02
        _Fade("Fade Softness", Range(0.0, 0.1)) = 0.005
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _BaseColor;
            float _Thickness;
            float _Fade;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                // Calculate distance to nearest edge (0 to 0.5)
                float distX = min(uv.x, 1.0 - uv.x);
                float distY = min(uv.y, 1.0 - uv.y);
                float dist = min(distX, distY);

                // Smoothstep for soft edge
                float alpha = 1.0 - smoothstep(_Thickness - _Fade, _Thickness, dist);
                
                return half4(_BaseColor.rgb, _BaseColor.a * alpha);
            }
            ENDHLSL
        }
    }
}
