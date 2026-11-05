Shader "Hidden/Tanuki/PreviewTonemapping"
{
    Properties { _MainTex("HDR Scene",2D)="black"{} _Exposure("Exposure",Float)=1 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _Exposure;
            float3 Film(float3 color)
            {
                // UE's default film slope/toe/shoulder (0.88, 0.55, 0.26),
                // evaluated in ACEScg. The full UE gamut/glow/LUT pipeline is not reproduced.
                const float3x3 toAP1=float3x3(.613097,.339523,.047380,.070194,.916354,.013452,.020616,.109570,.869815);
                const float3x3 fromAP1=float3x3(1.705051,-.621792,-.083259,-.130257,1.140805,-.010548,-.024003,-.128969,1.152972);
                float3 c=max(mul(toAP1,color),0);
                c=lerp(dot(c,float3(.272229,.674082,.053690)),c,.96);
                const float slope=.88,toeScale=.45,shoulderScale=.78;
                const float bt=.18/toeScale-1;
                const float toeMatch=-.7447275-.5*log((1+bt)/(1-bt))*(toeScale/slope);
                const float straightMatch=.45/slope-toeMatch;
                const float shoulderMatch=.26/slope-straightMatch;
                float3 logColor=log10(max(c,1e-6));
                float3 straight=slope*(logColor+straightMatch);
                float3 low=2*toeScale/(1+exp((-2*slope/toeScale)*(logColor-toeMatch)));
                float3 high=1.04-2*shoulderScale/(1+exp((2*slope/shoulderScale)*(logColor-shoulderMatch)));
                low=lerp(straight,low,step(logColor,toeMatch));
                high=lerp(straight,high,step(shoulderMatch,logColor));
                float3 t=saturate((logColor-toeMatch)/(shoulderMatch-toeMatch));
                if(shoulderMatch<toeMatch)t=1-t;
                c=lerp(low,high,t*t*(3-2*t));
                c=lerp(dot(c,float3(.272229,.674082,.053690)),c,.93);
                return saturate(mul(fromAP1,max(c,0)));
            }
            float4 frag(v2f_img input):SV_Target
            {
                float4 color=tex2D(_MainTex,input.uv);
                #ifdef UNITY_COLORSPACE_GAMMA
                color.rgb=GammaToLinearSpace(color.rgb);
                #endif
                color.rgb=Film(color.rgb*_Exposure);
                #ifdef UNITY_COLORSPACE_GAMMA
                color.rgb=LinearToGammaSpace(color.rgb);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
