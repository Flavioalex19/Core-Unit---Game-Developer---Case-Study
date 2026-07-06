Shader "Unlit/Shader_SDR_MaskedUiBrush"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" { }
        //_Color("Color", Color) = (1, 1, 1, 1)


        // Stencil Properties
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255

        _ColorMask("Color Mask", Float) = 15

    }
        SubShader
        {
            Tags
            {
                "RenderType" = "Transparent"
                 "Queue" = "Transparent"
            }
                 LOD 100

            Stencil
            {
                Ref[_Stencil]
                Comp[_StencilComp]
                Pass[_StencilOp]
                ReadMask[_StencilReadMask]
                WriteMask[_StencilWriteMask]
            }

            Pass
            {
                Blend SrcAlpha OneMinusSrcAlpha
                ZWrite Off
                Cull Off
                ColorMask[_ColorMask]

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            # include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color    : COLOR; // Get Vertex Color;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR; // Get Vertex Color;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            //Mask variables/Color
            sampler2D _MaskTex;
            //float4 _MaskTex_ST;
            //float4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color; // Vertex Color
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sampling Mask Texture 2D
                fixed4 maskCol = tex2D(_MaskTex, i.uv);
            // Sampling main texture
            fixed4 col = tex2D(_MainTex, i.uv);

            // Only need one channel from the mask after all.
            float mask = maskCol.r;

            // Apply vertex color (From Sprite Renderer) delimited by the mask, Allows certain parts of the object to remain unchanged (Handle for Example).
            col = lerp(col, col * i.color, mask);

            // apply fog
            UNITY_APPLY_FOG(i.fogCoord, col);
            return col;
        }
        ENDCG
    }
        }
}
