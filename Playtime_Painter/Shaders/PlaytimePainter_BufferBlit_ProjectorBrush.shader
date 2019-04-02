﻿Shader "Playtime Painter/Editor/Buffer Blit/Projector Brush"
{
	Category{

		 Tags{ "Queue" = "Transparent"}

		 ColorMask RGBA
		 Cull off
		 ZTest off
		 ZWrite off

		 SubShader{
			 Pass{

				 CGPROGRAM

				 #include "PlaytimePainter_cg.cginc"

				 #pragma multi_compile ____ TARGET_TRANSPARENT_LAYER

				 #pragma vertex vert
				 #pragma fragment frag

		struct v2f {
					float4 pos : POSITION;
					float4 texcoord : TEXCOORD0;
				};

				v2f vert(appdata_full v) {

					v2f o;
	
					o.pos = UnityObjectToClipPos(v.vertex);
					o.texcoord = brushTexcoord(v.texcoord.xy, v.vertex);

					return o;
				}

				float4 frag(v2f o) : COLOR{

					float4 buff = tex2Dlod(_pp_AlphaBuffer, float4(o.texcoord.xy ,0,0));//SampleAlphaBuffer(o.texcoord.xy);

					float4 src = tex2Dlod(_SourceTexture, float4(buff.rg, 0, 0));

					float alpha = min(1,buff.a);

					_brushColor.rgb = SourceTextureByBrush(src.rgb);

					#if TARGET_TRANSPARENT_LAYER
						return AlphaBlitTransparent(alpha, _brushColor,  o.texcoord.xy);
					#else
						return AlphaBlitOpaque(alpha, _brushColor,  o.texcoord.xy);
					#endif
				}
				 ENDCG
			 }
		 }
	}
}
