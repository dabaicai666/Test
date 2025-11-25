using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 简化版网络请求修复
/// 如果您觉得OptimizedWebRequest太复杂，可以使用这个版本
/// 这是对您原代码的最小改动版本
/// </summary>
public class SimpleWebRequestFix
{
    /// <summary>
    /// 简单修复版本 - 只添加超时和基础重试
    /// 这是对您原代码的最小改动
    /// </summary>
    public static async UniTask<string> PostWebRequestAsync(
        string url,
        string jsonData,
        Dictionary<string, string> requestHeaders = null,
        int timeoutSeconds = 30,          // ⭐ 新增：超时时间
        int maxRetries = 3,               // ⭐ 新增：重试次数
        CancellationToken cancellationToken = default) // ⭐ 新增：取消令牌
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            // 重试延迟
            if (attempt > 0)
            {
                Debug.LogWarning($"[网络请求] 重试 {attempt}/{maxRetries}, Url:{url}");
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);
            }

            using var webRequest = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            // ⭐ 关键修复1：设置超时
            webRequest.timeout = timeoutSeconds;

            webRequest.SetRequestHeader("Content-Type", "application/json");
            if (requestHeaders is { Count: > 0 })
            {
                foreach (var header in requestHeaders)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }
            }

            var operation = webRequest.SendWebRequest();

            try
            {
                // ⭐ 关键修复2：添加取消令牌支持
                await operation.ToUniTask(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"接口请求被取消,Url:{url}");
                webRequest.Abort();
                return null;
            }
            catch (Exception ex)
            {
                // ⭐ 关键修复3：捕获所有异常，避免卡死
                if (attempt < maxRetries)
                {
                    Debug.LogWarning($"接口请求异常,Url:{url}, Error:{ex.Message}, 将重试...");
                    continue; // 继续重试
                }
                else
                {
                    Debug.LogError($"接口请求失败（已重试{maxRetries}次）,Url:{url}, Error:{ex.Message}");
                    return null;
                }
            }

            // ⭐ 关键修复4：更详细的错误判断
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"Result:{webRequest.result}, Code:{webRequest.responseCode}, Error:{webRequest.error}";
                
                // 判断是否应该重试
                bool shouldRetry = webRequest.result == UnityWebRequest.Result.ConnectionError ||
                                 webRequest.result == UnityWebRequest.Result.DataProcessingError ||
                                 webRequest.responseCode >= 500 ||
                                 webRequest.responseCode == 0;

                if (shouldRetry && attempt < maxRetries)
                {
                    Debug.LogWarning($"接口请求失败,Url:{url}, {errorMsg}, 将重试...");
                    continue; // 继续重试
                }
                else
                {
                    Debug.LogError($"接口请求失败,Url:{url}, {errorMsg}");
                    return null;
                }
            }

            // 成功
            Debug.Log($"接口请求成功,Url:{url}, Response:{webRequest.downloadHandler.text}");
            return webRequest.downloadHandler.text;
        }

        // 所有重试都失败
        Debug.LogError($"接口请求失败（已重试{maxRetries}次）,Url:{url}");
        return null;
    }
}

/// <summary>
/// 使用示例
/// </summary>
public class SimpleWebRequestFixExample : MonoBehaviour
{
    // ========== 用法1：完全兼容您的原代码 ==========
    
    async void Example1_CompatibleUsage()
    {
        // 完全按照您原来的方式调用，只是换个类名
        var response = await SimpleWebRequestFix.PostWebRequestAsync(
            "https://api.example.com/data",
            "{\"key\":\"value\"}",
            new Dictionary<string, string> { { "Authorization", "Bearer token" } }
        );
        
        if (response != null)
        {
            Debug.Log($"成功：{response}");
        }
    }

    // ========== 用法2：自定义超时和重试 ==========
    
    async void Example2_CustomTimeout()
    {
        var response = await SimpleWebRequestFix.PostWebRequestAsync(
            "https://api.example.com/data",
            "{\"key\":\"value\"}",
            null,
            timeoutSeconds: 15,  // 15秒超时
            maxRetries: 5        // 最多重试5次
        );
        
        if (response != null)
        {
            Debug.Log("成功");
        }
    }

    // ========== 用法3：带取消令牌 ==========
    
    private CancellationTokenSource cts;
    
    async void Example3_WithCancellation()
    {
        cts = new CancellationTokenSource();
        
        var response = await SimpleWebRequestFix.PostWebRequestAsync(
            "https://api.example.com/data",
            "{}",
            null,
            cancellationToken: cts.Token
        );
        
        if (response != null)
        {
            Debug.Log("成功");
        }
    }
    
    void CancelRequest()
    {
        cts?.Cancel();
    }

    // ========== 用法4：替换您原来的方法 ==========
    
    // 原来的方法：
    /*
    private async UniTask<string> PostWebRequestAsync(string url, string jsonData,
        Dictionary<string, string> requestHeaders = null)
    {
        using var webRequest = new UnityWebRequest(url, "Post");
        // ... 原代码 ...
    }
    */
    
    // 新的方法（保持完全兼容）：
    private async UniTask<string> PostWebRequestAsync(
        string url,
        string jsonData,
        Dictionary<string, string> requestHeaders = null)
    {
        // ⭐ 只需要这一行改动！
        return await SimpleWebRequestFix.PostWebRequestAsync(
            url,
            jsonData,
            requestHeaders,
            timeoutSeconds: 30,  // 可以调整
            maxRetries: 3        // 可以调整
        );
    }
    
    // 然后您原来的所有调用代码都不需要改！
    async void YourOriginalCode()
    {
        var result = await PostWebRequestAsync(
            "https://api.example.com/data",
            "{\"key\":\"value\"}",
            new Dictionary<string, string> { { "Auth", "token" } }
        );
        
        if (result != null)
        {
            // 处理结果
        }
    }
}
