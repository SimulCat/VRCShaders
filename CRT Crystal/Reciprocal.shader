Shader"SimulCat/Crystal/ReciprocalBees"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_CellSize ("CellSize", Vector) = (1,1,1,1)
        _MaxImpulse ("Max Particle Impulse", Range(5,15)) = 10
        _ParticleImpulse ("Particle Impulse", Range(0,15)) = 0
	}

	SubShader
	{
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend One One
		LOD 100
		ZWrite on
		Cull Off
		
		CGINCLUDE
			
			#include "UnityCG.cginc"
			
			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
				uint id : SV_VertexID;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
				float3 color : TEXCOORD2;
				UNITY_FOG_COORDS(1)
				//V2F_SHADOW_CASTER;
				float4 pos : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
            float4 _CellSize; //xyz dims w=type (0=rectangle,1=face centred, 2=body centered)
			float _MaxImpulse;
			float _ParticleImpulse;

			
			v2f vert (appdata v)
			{
				v2f o;


				uint id = v.id/4;
				uint idSub = (v.id )%4;

				o.color = float3(0.5,0.5,0.5);
				float3 offset = float3(
					0,
					0,
					0
				);
				
				
				float size = 0.01;
				float3 vertSubOffset;
				switch(idSub){
					case 1:
						vertSubOffset = float3(0, size, 0);
						break;
					case 2:
						vertSubOffset = float3(size, 0, 0);
						break;
					default:
						vertSubOffset = float3(-size, -size, 0);
						break;
				}

				offset *= max(0.2,length(unity_ObjectToWorld._m00_m10_m20));
				v.vertex.xyz = mul(unity_WorldToObject, offset);

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
		ENDCG
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog

			

			float4 frag (v2f i) : SV_Target
			{
				// sample the texture
				float4 col = tex2D(_MainTex, i.uv);
				if(col.a <= 0){
					clip(-1);
					//col = 0;
				}
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
		Pass
		{
			Tags {"LightMode"="ShadowCaster"}

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_shadowcaster
			#include "UnityCG.cginc"


			float4 frag(v2f i) : SV_Target
			{
				if(distance(i.uv, float2(0.3,0.3)) > 0.3){
					clip(-1);
				}
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}
	}
}
