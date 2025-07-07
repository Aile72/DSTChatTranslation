using DSTChatTranslation.Models;
using System.Net;
using System.Net.Http;

namespace DSTChatTranslation.Helpers;

/// <summary>
/// 自定义HttpClientHandler功能
/// </summary>
public class CustomHttpClientHandler : HttpClientHandler
{
	// 静态变量缓存重定向结果
	private static string? _bingRedirectTarget;
	private static readonly object _lock = new();
	private static DateTime _lastChecked = DateTime.MinValue;
	private static string? _lastProxyUrl;
	private static bool? _lastUseProxy;

	/// <summary>
	/// 自定义HttpClientHandler构造函数
	/// </summary>
	/// <param name="request"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentNullException"></exception>
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request, CancellationToken cancellationToken)
	{
		if (request?.RequestUri == null)
			throw new ArgumentNullException(nameof(request));

		// 自动检测Bing重定向
		if (ShouldHandleBingRequest(request.RequestUri))
		{
			// 获取重定向目标
			var redirectTarget = GetBingRedirectTarget(request, cancellationToken);

			if (!string.IsNullOrEmpty(redirectTarget))
			{
				// 替换请求为检测到的目标域名
				var newUri = new UriBuilder(request.RequestUri)
				{
					Host = redirectTarget
				}.Uri;

				request.RequestUri = newUri;
			}
		}

		return await base.SendAsync(request, cancellationToken);
	}

	/// <summary>
	/// 判断是否为Bing请求
	/// </summary>
	private static bool ShouldHandleBingRequest(Uri uri)
	{
		return uri.Host.EndsWith("bing.com", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 获取Bing重定向目标
	/// </summary>
	private static string? GetBingRedirectTarget(
		HttpRequestMessage originalRequest,
		CancellationToken cancellationToken)
	{
		// 检查代理设置是否变更
		bool proxyChanged = _lastProxyUrl != AppSettingsModel.Current.ProxyUrl ||
							_lastUseProxy != AppSettingsModel.Current.UseProxy;

		// 如果代理设置变更或缓存过期（10分钟），重置缓存
		if (proxyChanged || (DateTime.UtcNow - _lastChecked).TotalMinutes > 10)
		{
			lock (_lock)
			{
				if (proxyChanged || (DateTime.UtcNow - _lastChecked).TotalMinutes > 10)
				{
					_bingRedirectTarget = null;
					_lastChecked = DateTime.MinValue;

					// 更新代理设置记录
					_lastProxyUrl = AppSettingsModel.Current.ProxyUrl;
					_lastUseProxy = AppSettingsModel.Current.UseProxy;
				}
			}
		}

		// 如果缓存有效，直接返回
		if (_bingRedirectTarget != null &&
			(DateTime.UtcNow - _lastChecked).TotalMinutes < 10)
			return _bingRedirectTarget;

		lock (_lock)
		{
			// 双重检查锁
			if (_bingRedirectTarget != null &&
				(DateTime.UtcNow - _lastChecked).TotalMinutes < 10)
				return _bingRedirectTarget;

			try
			{
				// 使用临时HttpClient检测重定向
				using var tempClient = CreateProbingClient();

				// 创建探测请求
				var probeRequest = CreateProbeRequest(originalRequest);

				// 发送探测请求
				var response = tempClient.Send(probeRequest, cancellationToken);

				// 处理重定向响应
				_bingRedirectTarget = ProcessRedirectResponse(response);

				// 更新检测时间
				_lastChecked = DateTime.UtcNow;
			}
			catch
			{
				// 探测失败时保持原样
			}
		}

		return _bingRedirectTarget;
	}

	/// <summary>
	/// 创建探测客户端
	/// </summary>
	private static HttpClient CreateProbingClient()
	{
		// 创建自定义HttpClientHandler
		var handler = new HttpClientHandler
		{
			AllowAutoRedirect = false, // 禁用自动重定向
			UseProxy = AppSettingsModel.Current.UseProxy
		};

		// 配置代理
		if (AppSettingsModel.Current.UseProxy &&
			!string.IsNullOrEmpty(AppSettingsModel.Current.ProxyUrl))
		{
			handler.Proxy = new WebProxy(AppSettingsModel.Current.ProxyUrl);
		}

		return new HttpClient(handler);
	}

	/// <summary>
	/// 创建探测请求
	/// </summary>
	private static HttpRequestMessage CreateProbeRequest(HttpRequestMessage originalRequest)
	{
		// 复制原始请求（不含内容）
		var probeRequest = new HttpRequestMessage(
			HttpMethod.Head, // 使用HEAD方法减少数据传输
			originalRequest.RequestUri);

		// 复制重要标头
		foreach (var header in originalRequest.Headers)
		{
			probeRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}

		// 添加标准用户代理
		probeRequest.Headers.UserAgent.ParseAdd(
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
			"AppleWebKit/537.36 (KHTML, like Gecko) " +
			"Chrome/125.0.0.0 Safari/537.36");

		return probeRequest;
	}

	/// <summary>
	/// 处理重定向响应
	/// </summary>
	private static string? ProcessRedirectResponse(HttpResponseMessage response)
	{
		// 处理重定向响应 (3xx)
		if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
		{
			if (response.Headers.Location != null)
			{
				var location = response.Headers.Location;
				if (!string.IsNullOrEmpty(location.Host))
				{
					return location.Host;
				}
			}
		}
		// 处理直接访问的情况
		else if (response.RequestMessage?.RequestUri != null)
		{
			return response.RequestMessage.RequestUri.Host;
		}

		return null;
	}
}
