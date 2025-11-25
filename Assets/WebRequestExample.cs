using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 网络请求使用示例
/// </summary>
public class WebRequestExample : MonoBehaviour
{
    // ========== 示例1：基础使用（最简单） ==========
    
    async void Example1_BasicUsage()
    {
        string url = "https://api.example.com/data";
        string jsonData = "{\"key\":\"value\"}";
        
        // 使用默认配置（30秒超时，3次重试）
        var result = await OptimizedWebRequest.PostAsync(url, jsonData);
        
        if (result.Success)
        {
            Debug.Log($"成功：{result.Data}");
        }
        else
        {
            Debug.LogError($"失败：{result.ErrorMessage}");
        }
    }

    // ========== 示例2：自定义配置 ==========
    
    async void Example2_CustomConfig()
    {
        string url = "https://api.example.com/data";
        string jsonData = "{\"key\":\"value\"}";
        
        // 自定义配置
        var config = new OptimizedWebRequest.RequestConfig
        {
            TimeoutSeconds = 15,        // 15秒超时
            MaxRetryCount = 5,          // 最多重试5次
            RetryDelaySeconds = 2f,     // 每次重试间隔2秒
            UseExponentialBackoff = true, // 使用指数退避
            LogErrorDetails = true,      // 记录详细错误
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer your_token" },
                { "X-Custom-Header", "custom_value" }
            }
        };
        
        var result = await OptimizedWebRequest.PostAsync(url, jsonData, config);
        
        if (result.Success)
        {
            Debug.Log($"请求成功（重试了{result.RetryCount}次）");
            Debug.Log($"数据：{result.Data}");
        }
        else
        {
            if (result.WasTimeout)
            {
                Debug.LogError("请求超时");
            }
            else if (result.WasCancelled)
            {
                Debug.LogWarning("请求被取消");
            }
            else
            {
                Debug.LogError($"请求失败：{result.ErrorMessage}");
                Debug.LogError($"HTTP状态码：{result.ResponseCode}");
            }
        }
    }

    // ========== 示例3：带取消令牌 ==========
    
    private CancellationTokenSource cancellationTokenSource;
    
    async void Example3_WithCancellation()
    {
        cancellationTokenSource = new CancellationTokenSource();
        
        string url = "https://api.example.com/data";
        string jsonData = "{\"key\":\"value\"}";
        
        try
        {
            var result = await OptimizedWebRequest.PostAsync(
                url, 
                jsonData, 
                cancellationToken: cancellationTokenSource.Token
            );
            
            if (result.Success)
            {
                Debug.Log("成功");
            }
            else if (result.WasCancelled)
            {
                Debug.LogWarning("请求被取消");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("请求被取消（异常）");
        }
    }
    
    // 取消请求
    void CancelRequest()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }

    // ========== 示例4：带进度回调 ==========
    
    async void Example4_WithProgress()
    {
        string url = "https://api.example.com/data";
        string jsonData = "{\"key\":\"value\"}";
        
        // 创建进度报告器
        var progress = new Progress<float>(value =>
        {
            Debug.Log($"上传进度：{value * 100:F1}%");
            // 更新UI进度条等
        });
        
        var result = await OptimizedWebRequest.PostAsync(
            url, 
            jsonData, 
            progress: progress
        );
        
        if (result.Success)
        {
            Debug.Log("上传完成");
        }
    }

    // ========== 示例5：弱网环境测试 ==========
    
    async void Example5_WeakNetworkTest()
    {
        string url = "https://httpstat.us/200?sleep=5000"; // 模拟慢速服务器
        string jsonData = "{}";
        
        var config = new OptimizedWebRequest.RequestConfig
        {
            TimeoutSeconds = 10,     // 10秒超时
            MaxRetryCount = 2,       // 最多重试2次
            RetryDelaySeconds = 1f,
            UseExponentialBackoff = true
        };
        
        Debug.Log("开始弱网测试...");
        var startTime = Time.realtimeSinceStartup;
        
        var result = await OptimizedWebRequest.PostAsync(url, jsonData, config);
        
        var duration = Time.realtimeSinceStartup - startTime;
        
        Debug.Log($"测试完成，耗时：{duration:F2}秒");
        Debug.Log($"重试次数：{result.RetryCount}");
        
        if (result.Success)
        {
            Debug.Log("✅ 弱网环境测试成功");
        }
        else
        {
            Debug.LogWarning($"❌ 弱网环境测试失败：{result.ErrorMessage}");
        }
    }

    // ========== 示例6：批量请求 ==========
    
    async void Example6_MultipleRequests()
    {
        var urls = new[]
        {
            "https://api.example.com/endpoint1",
            "https://api.example.com/endpoint2",
            "https://api.example.com/endpoint3"
        };
        
        var tasks = new List<UniTask<OptimizedWebRequest.RequestResult>>();
        
        foreach (var url in urls)
        {
            tasks.Add(OptimizedWebRequest.PostAsync(url, "{}"));
        }
        
        // 并发执行所有请求
        var results = await UniTask.WhenAll(tasks);
        
        int successCount = 0;
        foreach (var result in results)
        {
            if (result.Success)
                successCount++;
        }
        
        Debug.Log($"批量请求完成：{successCount}/{urls.Length} 成功");
    }

    // ========== 示例7：带超时的用户友好提示 ==========
    
    async void Example7_UserFriendlyTimeout()
    {
        string url = "https://api.example.com/data";
        string jsonData = "{\"key\":\"value\"}";
        
        var config = new OptimizedWebRequest.RequestConfig
        {
            TimeoutSeconds = 10,
            MaxRetryCount = 2
        };
        
        // 显示加载UI
        ShowLoadingUI("正在连接服务器...");
        
        var result = await OptimizedWebRequest.PostAsync(url, jsonData, config);
        
        // 隐藏加载UI
        HideLoadingUI();
        
        if (result.Success)
        {
            ShowSuccessMessage("数据加载成功！");
        }
        else
        {
            if (result.WasTimeout)
            {
                ShowErrorMessage("网络连接超时\n请检查网络连接后重试");
            }
            else if (result.ResponseCode == 0)
            {
                ShowErrorMessage("无法连接到服务器\n请检查网络设置");
            }
            else
            {
                ShowErrorMessage($"请求失败\n错误代码：{result.ResponseCode}");
            }
        }
    }
    
    // UI辅助方法（示例）
    void ShowLoadingUI(string message) => Debug.Log($"[UI] {message}");
    void HideLoadingUI() => Debug.Log("[UI] 隐藏加载");
    void ShowSuccessMessage(string message) => Debug.Log($"[UI] ✅ {message}");
    void ShowErrorMessage(string message) => Debug.LogWarning($"[UI] ❌ {message}");

    // ========== 示例8：迁移您的原代码 ==========
    
    // 您的原代码：
    /*
    private async UniTask<string> PostWebRequestAsync(string url, string jsonData,
        Dictionary<string, string> requestHeaders = null)
    {
        // ... 原代码 ...
    }
    */
    
    // 迁移后的代码（保持接口兼容）：
    private async UniTask<string> PostWebRequestAsync(
        string url, 
        string jsonData,
        Dictionary<string, string> requestHeaders = null)
    {
        var config = new OptimizedWebRequest.RequestConfig
        {
            TimeoutSeconds = 30,    // ⭐ 添加超时
            MaxRetryCount = 3,      // ⭐ 添加重试
            Headers = requestHeaders ?? new Dictionary<string, string>()
        };
        
        var result = await OptimizedWebRequest.PostAsync(url, jsonData, config);
        
        if (result.Success)
        {
            Debug.Log($"接口请求成功,Url:{url}, Response:{result.Data}");
            return result.Data;
        }
        else
        {
            Debug.LogError($"接口请求失败,Url:{url}, Error:{result.ErrorMessage}");
            return null;
        }
    }

    // ========== 示例9：使用MonoBehaviour生命周期自动取消 ==========
    
    async void Example9_AutoCancelOnDestroy()
    {
        // 使用destroyCancellationToken，当GameObject销毁时自动取消
        var result = await OptimizedWebRequest.PostAsync(
            "https://api.example.com/data",
            "{}",
            cancellationToken: this.destroyCancellationToken
        );
        
        // 如果GameObject在请求完成前被销毁，这里不会执行
        if (result.Success)
        {
            Debug.Log("成功");
        }
    }
}
