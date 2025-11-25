Shader "Example/Forward Rendering Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        
        // ========================================
        // Pass 1: ForwardBase Pass
        // 处理：主光源 + 环境光 + 自发光 + 光照贴图
        // ========================================
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }  // ⭐ 关键：指定为ForwardBase
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // ⭐ Forward Base的编译指令
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                SHADOW_COORDS(3)  // ⭐ 阴影坐标
                UNITY_FOG_COORDS(4)
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Glossiness;
            half _Metallic;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                TRANSFER_SHADOW(o);  // ⭐ 传递阴影
                UNITY_TRANSFER_FOG(o, o.pos);
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // 基础颜色
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                
                // 法线
                float3 worldNormal = normalize(i.worldNormal);
                float3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 worldViewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                
                // ⭐ 计算主光源光照（Lambert漫反射）
                fixed NdotL = max(0, dot(worldNormal, worldLightDir));
                fixed3 diffuse = _LightColor0.rgb * albedo.rgb * NdotL;
                
                // ⭐ 环境光
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * albedo.rgb;
                
                // ⭐ 阴影
                fixed shadow = SHADOW_ATTENUATION(i);
                
                // 最终颜色 = 环境光 + (漫反射 × 阴影)
                fixed3 finalColor = ambient + diffuse * shadow;
                
                // 雾效
                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                
                return fixed4(finalColor, albedo.a);
            }
            ENDCG
        }
        
        // ========================================
        // Pass 2: ForwardAdd Pass
        // 处理：每个附加光源（点光源、聚光灯等）
        // 这个Pass会被执行多次（每个附加光源一次）
        // ========================================
        Pass
        {
            Name "FORWARD_ADD"
            Tags { "LightMode"="ForwardAdd" }  // ⭐ 关键：指定为ForwardAdd
            
            // ⭐ 关键：使用加法混合，累加光照
            Blend One One
            ZWrite Off  // 不写入深度
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // ⭐ Forward Add的编译指令
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                
                float3 worldNormal = normalize(i.worldNormal);
                float3 worldPos = i.worldPos;
                
                // ⭐ 计算光源方向（点光源和聚光灯的计算方式不同）
                #ifdef USING_DIRECTIONAL_LIGHT
                    float3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz);
                    fixed atten = 1.0;
                #else
                    // 点光源或聚光灯
                    float3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz - worldPos);
                    float distance = length(_WorldSpaceLightPos0.xyz - worldPos);
                    
                    // ⭐ 光照衰减（根据距离）
                    UNITY_LIGHT_ATTENUATION(atten, i, worldPos);
                #endif
                
                // ⭐ 计算当前光源的光照
                fixed NdotL = max(0, dot(worldNormal, worldLightDir));
                fixed3 diffuse = _LightColor0.rgb * albedo.rgb * NdotL * atten;
                
                // 雾效
                UNITY_APPLY_FOG(i.fogCoord, diffuse);
                
                return fixed4(diffuse, 0);  // Alpha为0，因为使用加法混合
            }
            ENDCG
        }
        
        // ========================================
        // Pass 3: ShadowCaster Pass
        // 用于投射阴影
        // ========================================
        Pass
        {
            Name "SHADOW_CASTER"
            Tags { "LightMode"="ShadowCaster" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            
            #include "UnityCG.cginc"
            
            struct v2f
            {
                V2F_SHADOW_CASTER;
            };
            
            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    
    // 降级到移动平台Shader
    FallBack "Mobile/Diffuse"
}
