Shader "FluidSim/Advect" 
{
	SubShader 
	{
    	Pass 
    	{
			ZTest Always

			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			
			uniform sampler2D _Velocity;
			uniform sampler2D _Source;
			uniform sampler2D _Obstacles;
			
			uniform float2 _InverseSize;
			uniform float _TimeStep;
			uniform float _Dissipation;
		
			struct v2f 
			{
    			float4 pos : SV_POSITION;
    			float2 uv : TEXCOORD0;
			};

			v2f vert(appdata_base v)
			{
    			v2f OUT;
    			OUT.pos = UnityObjectToClipPos(v.vertex);
    			OUT.uv = v.texcoord.xy;
    			return OUT;
			}
			
			float4 frag(v2f IN) : COLOR
			{
			
			    float2 u = tex2D(_Velocity, IN.uv).xy;
			    
			    float2 coord = IN.uv - u * _InverseSize * _TimeStep;
			    
			    float solid = tex2D(_Obstacles, IN.uv).x;
			    
			    return solid > 0.0 ? 0 : _Dissipation * tex2D(_Source, coord);
			}
			
			ENDCG

    	}
	}
}