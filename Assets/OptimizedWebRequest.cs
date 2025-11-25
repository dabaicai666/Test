using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 优化的网络请求工具类
/// 特点：
/// 1. 超时控制
/// 2. 自动重试
/// 3. 取消令牌支持
/// 4. 详细的错误处理
/// 5. 进度回调
/// </summary>
public class OptimizedWebRequest
{
    /// <summary>
    /// 请求配置
    /// </summary>
    public class RequestConfig
    {
        /// <summary>
        /// 超时时间（秒），默认30秒
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// 最大重试次数，默认3次
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;
        
        /// <summary>
        /// 重试间隔（秒），默认1秒
        /// </summary>
        public float RetryDelaySeconds { get; set; } = 1f;
        
        /// <summary>
        /// 是否使用指数退避（每次重试延迟翻倍）
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;
        
        /// <summary>
        /// 是否在错误时记录详细日志
        /// </summary>
        public bool LogErrorDetails { get; set; } = true;
        
        /// <summary>
        /// 请求头
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }
    
    /// <summary>
    /// 请求结果
    /// </summary>
    public class RequestResult
    {
        public bool Success { get; set; }
        public string Data { get; set; }
        public string ErrorMessage { get; set; }
        public long ResponseCode { get; set; }
        public int RetryCount { get; set; }
        public bool WasCancelled { get; set; }
        public bool WasTimeout { get; set; }
    }

    /// <summary>
    /// 优化的POST请求（带超时和重试）
    /// </summary>
    public static async UniTask<RequestResult> PostAsync(
        string url,
        string jsonData,
        RequestConfig config = null,
        IProgress<float> progress = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new RequestConfig();
        
        var result = new RequestResult
        {
            Success = false,
            RetryCount = 0
        };

        // 重试循环
        for (int attempt = 0; attempt <= config.MaxRetryCount; attempt++)
        {
            result.RetryCount = attempt;
            
            if (attempt > 0)
            {
                // 计算重试延迟
                float delay = config.UseExponentialBackoff 
                    ? config.RetryDelaySeconds * Mathf.Pow(2, attempt - 1)
                    : config.RetryDelaySeconds;
                
                Debug.LogWarning($"[网络请求] 第{attempt}次重试，等待{delay:F1}秒... Url:{url}");
                
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    result.WasCancelled = true;
                    result.ErrorMessage = "请求被取消";
                    return result;
                }
            }

            // 执行单次请求
            var singleResult = await ExecuteSingleRequestAsync(
                url, 
                jsonData, 
                config, 
                progress, 
                cancellationToken
            );

            // 检查结果
            if (singleResult.Success)
            {
                return singleResult; // 成功，直接返回
            }

            // 记录失败
            result = singleResult;
            
            // 判断是否应该重试
            if (singleResult.WasCancelled)
            {
                return result; // 被取消，不重试
            }
            
            if (!ShouldRetry(singleResult))
            {
                return result; // 不应该重试的错误（如404）
            }
            
            Debug.LogWarning($"[网络请求] 第{attempt + 1}次尝试失败: {singleResult.ErrorMessage}");
        }

        // 所有重试都失败
        result.ErrorMessage = $"请求失败（已重试{config.MaxRetryCount}次）: {result.ErrorMessage}";
        return result;
    }

    /// <summary>
    /// 执行单次请求
    /// </summary>
    private static async UniTask<RequestResult> ExecuteSingleRequestAsync(
        string url,
        string jsonData,
        RequestConfig config,
        IProgress<float> progress,
        CancellationToken cancellationToken)
    {
        var result = new RequestResult
        {
            Success = false
        };

        UnityWebRequest webRequest = null;
        
        try
        {
            // 创建请求
            webRequest = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            
            // ⭐ 关键优化1：设置超时
            webRequest.timeout = config.TimeoutSeconds;

            // 设置请求头
            webRequest.SetRequestHeader("Content-Type", "application/json");
            foreach (var header in config.Headers)
            {
                webRequest.SetRequestHeader(header.Key, header.Value);
            }

            // 发送请求
            var operation = webRequest.SendWebRequest();

            // ⭐ 关键优化2：同时监听超时和取消
            var timeoutCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                timeoutCts.Token
            );

            try
            {
                // 创建多个等待任务
                var requestTask = operation.ToUniTask(
                    progress: progress,
                    cancellationToken: linkedCts.Token
                );
                
                var timeoutTask = UniTask.Delay(
                    TimeSpan.FromSeconds(config.TimeoutSeconds), 
                    cancellationToken: linkedCts.Token
                );

                // ⭐ 关键优化3：使用WhenAny，哪个先完成就用哪个
                var completedTaskIndex = await UniTask.WhenAny(requestTask, timeoutTask);

                // 检查是否超时
                if (completedTaskIndex == 1)
                {
                    result.WasTimeout = true;
                    result.ErrorMessage = $"请求超时（{config.TimeoutSeconds}秒）";
                    
                    // 取消请求
                    webRequest.Abort();
                    
                    if (config.LogErrorDetails)
                    {
                        Debug.LogError($"[网络请求超时] Url:{url}, Timeout:{config.TimeoutSeconds}秒");
                    }
                    
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                result.WasCancelled = true;
                result.ErrorMessage = "请求被用户取消";
                webRequest.Abort();
                return result;
            }
            finally
            {
                timeoutCts?.Cancel();
                timeoutCts?.Dispose();
                linkedCts?.Dispose();
            }

            // ⭐ 关键优化4：详细的错误判断
            result.ResponseCode = webRequest.responseCode;
            
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                result.Success = true;
                result.Data = webRequest.downloadHandler.text;
                
                Debug.Log($"[网络请求成功] Url:{url}, ResponseCode:{result.ResponseCode}");
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = GetDetailedErrorMessage(webRequest);
                
                if (config.LogErrorDetails)
                {
                    Debug.LogError($"[网络请求失败] Url:{url}\n" +
                                 $"Result:{webRequest.result}\n" +
                                 $"ResponseCode:{result.ResponseCode}\n" +
                                 $"Error:{webRequest.error}\n" +
                                 $"详细信息:{result.ErrorMessage}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"请求异常: {ex.Message}";
            
            if (config.LogErrorDetails)
            {
                Debug.LogError($"[网络请求异常] Url:{url}\n异常:{ex}");
            }
        }
        finally
        {
            webRequest?.Dispose();
        }

        return result;
    }

    /// <summary>
    /// 获取详细的错误信息
    /// </summary>
    private static string GetDetailedErrorMessage(UnityWebRequest webRequest)
    {
        var sb = new StringBuilder();
        
        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                sb.AppendLine("连接错误：无法连接到服务器");
                sb.AppendLine("可能原因：");
                sb.AppendLine("- 网络未连接");
                sb.AppendLine("- 服务器地址错误");
                sb.AppendLine("- 防火墙阻止");
                break;
                
            case UnityWebRequest.Result.ProtocolError:
                sb.AppendLine($"协议错误：HTTP {webRequest.responseCode}");
                
                switch (webRequest.responseCode)
                {
                    case 400:
                        sb.AppendLine("错误请求：请求参数有误");
                        break;
                    case 401:
                        sb.AppendLine("未授权：需要身份验证");
                        break;
                    case 403:
                        sb.AppendLine("禁止访问：没有权限");
                        break;
                    case 404:
                        sb.AppendLine("未找到：请求的资源不存在");
                        break;
                    case 500:
                        sb.AppendLine("服务器内部错误");
                        break;
                    case 502:
                        sb.AppendLine("网关错误");
                        break;
                    case 503:
                        sb.AppendLine("服务不可用");
                        break;
                    case 504:
                        sb.AppendLine("网关超时");
                        break;
                }
                break;
                
            case UnityWebRequest.Result.DataProcessingError:
                sb.AppendLine("数据处理错误：接收或处理数据时出错");
                break;
        }
        
        sb.AppendLine($"原始错误：{webRequest.error}");
        
        return sb.ToString();
    }

    /// <summary>
    /// 判断是否应该重试
    /// </summary>
    private static bool ShouldRetry(RequestResult result)
    {
        // 已取消，不重试
        if (result.WasCancelled)
            return false;

        // 超时，应该重试
        if (result.WasTimeout)
            return true;

        // 根据HTTP状态码判断
        switch (result.ResponseCode)
        {
            case 0: // 网络错误
            case 408: // 请求超时
            case 429: // 请求过多
            case 500: // 服务器内部错误
            case 502: // 网关错误
            case 503: // 服务不可用
            case 504: // 网关超时
                return true;
                
            case 400: // 错误请求
            case 401: // 未授权
            case 403: // 禁止访问
            case 404: // 未找到
                return false; // 客户端错误，不应重试
                
            default:
                return result.ResponseCode >= 500; // 5xx错误重试
        }
    }
}
