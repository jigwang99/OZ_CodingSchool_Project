Shader "PixelTactics/Unit Star Outline"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture",2D)="white" {}
        _OutlineColor("Outline Color",Color)=(0,0,0,0)
        _OutlinePixels("Outline Pixels",Float)=1
        _UVBounds("Sprite UV Bounds",Vector)=(0,0,1,1)
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlinePixels;
                float4 _UVBounds;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0;};
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; return output;
            }
            half Alpha(float2 uv)
            {
                float2 inside=step(_UVBounds.xy,uv)*step(uv,_UVBounds.zw);
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a*inside.x*inside.y;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 pixel=_MainTex_TexelSize.xy*_OutlinePixels;
                half border=0;
                border=max(border,Alpha(input.uv+float2(pixel.x,0)));
                border=max(border,Alpha(input.uv-float2(pixel.x,0)));
                border=max(border,Alpha(input.uv+float2(0,pixel.y)));
                border=max(border,Alpha(input.uv-float2(0,pixel.y)));
                border=max(border,Alpha(input.uv+pixel));
                border=max(border,Alpha(input.uv-pixel));
                border=max(border,Alpha(input.uv+float2(pixel.x,-pixel.y)));
                border=max(border,Alpha(input.uv+float2(-pixel.x,pixel.y)));
                return half4(_OutlineColor.rgb,border*(1-Alpha(input.uv))*_OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
