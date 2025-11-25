Shader "Example/URP Forward Rendering Shader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMap ("Base Texture", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"  // ⭐ 指定URP管线
            "Queue"="Geometry"
        }
        
        LOD 200
        
        // ========================================
        // Pass 1: UniversalForward Pass
        // 特点：在一个Pass中处理所有光源！
        // 包括：主光源 + 多个附加光源（0-8个）
        // ========================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }  // ⭐ URP的Forward Pass
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // ⭐ URP的关键编译指令
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS          // 主光源阴影
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE  // 级联阴影
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS  // 附加光源
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS    // 附加光源阴影
            #pragma multi_compile_fog
            
            // ⭐ 包含URP的标准库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // 属性定义（使用CBUFFER以支持SRP Batcher）
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // ⭐ URP提供的辅助函数
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 基础颜色
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = baseMap * _BaseColor;
                
                // 世界空间数据
                float3 positionWS = input.positionWS;
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(GetCameraPositionWS() - positionWS);
                
                // ⭐ 创建InputData结构（URP光照所需）
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirectionWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.fogCoord = input.fogFactor;
                
                // ⭐ 创建SurfaceData结构
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.alpha = albedo.a;
                
                // ⭐ 关键：URP的UniversalFragmentPBR函数
                // 在一个函数中处理所有光源！
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                // 应用雾效
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                
                return color;
            }
            ENDHLSLPROGRAM
        }
        
        // ========================================
        // Pass 2: ShadowCaster Pass
        // 用于投射阴影
        // ========================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            float3 _LightDirection;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // ⭐ 应用阴影偏移
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                output.positionCS = positionCS;
                return output;
            }
            
            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSLPROGRAM
        }
        
        // ========================================
        // Pass 3: DepthOnly Pass（可选）
        // 用于深度预通道
        // ========================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 position : POSITION;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }
            
            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSLPROGRAM
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
