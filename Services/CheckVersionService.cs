using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Windows;
using Application = System.Windows.Application;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using MessageBox = System.Windows.MessageBox;
using DSTChatTranslation.Helpers;
using DSTChatTranslation.Models;
using DSTChatTranslation.Views;

namespace DSTChatTranslation.Services;

public partial class CheckVersionService
{
	private readonly HttpClient _httpClient;
	private readonly string _workshopItemUrl;
	private readonly string _currentVersion;
	private readonly SynchronizationContext? _syncContext;

	// 添加事件声明
	public event EventHandler? UpdateConfirmed;

	// 公开 WorkshopItemUrl 属性
	public string WorkshopItemUrl => _workshopItemUrl;

	public CheckVersionService(
		string workshopItemUrl,
		string currentVersion,
		SynchronizationContext? syncContext = null,
		int timeoutSeconds = 30)
	{
		_workshopItemUrl = workshopItemUrl;
		_currentVersion = currentVersion;
		// 捕获UI线程上下文用于消息框显示
		_syncContext = syncContext ?? SynchronizationContext.Current;

		var handler = new HttpClientHandler
		{
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
			UseCookies = false
		};

		_httpClient = new HttpClient(handler)
		{
			Timeout = TimeSpan.FromSeconds(timeoutSeconds)
		};

		// 设置浏览器请求头
		_httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
		_httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
		_httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
	}

	/// <summary>
	/// 检查更新并显示结果提示框
	/// </summary>
	public async Task CheckForUpdatesAsync(bool isManualCheck = false)
	{
		try
		{
			// 检查是否达到检查间隔（仅自动检查时生效）
			if (!isManualCheck && DateTime.Now - AppSettingsModel.Current.LastUpdateCheckTime < TimeSpan.FromDays(7))
			{
				Debug.WriteLine("尚未达到更新检查间隔（7天），跳过自动检查");
				return;
			}

			string html = await DownloadHtmlWithTimeoutAsync(CancellationToken.None);
			string onlineVersion = ParseVersionFromHtml(html);

			Debug.WriteLine($"在线版本: {onlineVersion}, 当前版本: {_currentVersion}");

			if (IsNewerVersion(onlineVersion, _currentVersion))
			{
				bool confirmed = ShowConfirmMessage(
					$"New version available: {onlineVersion}\nYour version: {_currentVersion}\n\nDo you want to update now?",
					string.Empty,
					MessageBoxIcon.Information);

				if (confirmed)
				{
					UpdateConfirmed?.Invoke(this, EventArgs.Empty);
				}
			}
			else if (isManualCheck) // 手动检查时显示最新版本提示
			{
				ShowMessage($"You have the latest version\n\nWorkshop version: {onlineVersion}\nYour version: {_currentVersion}",
							string.Empty, MessageBoxIcon.Information);
			}
		}
		catch (Exception ex)
		{
			if (isManualCheck) // 仅手动检查显示错误提示
			{
				bool confirmed = ShowConfirmMessage(
					"Check version failed: Please check your internet connection and try again later\n\nDo you want to update now?",
					string.Empty,
					MessageBoxIcon.Error);

				if (confirmed)
				{
					UpdateConfirmed?.Invoke(this, EventArgs.Empty);
				}
			}
			else
			{
				Debug.WriteLine($"自动更新检查失败: {ex.Message}");
			}
		}
		finally
		{
			// 仅自动检查更新最后检查时间
			if (!isManualCheck)
			{
				AppSettingsModel.Current.LastUpdateCheckTime = DateTime.Now;
				MainWindow.SaveSettings();
				Debug.WriteLine($"已更新最后检查时间: {AppSettingsModel.Current.LastUpdateCheckTime}");
			}
		}
	}

	/// <summary>
	/// 显示带确认按钮的消息框
	/// </summary>
	private static bool ShowConfirmMessage(string message, string title, MessageBoxIcon icon)
	{
		MessageBoxResult? result = null;

		Application.Current.Dispatcher.Invoke(() =>
		{
			result = MessageBox.Show(
				message,
				title,
				MessageBoxButton.YesNo,
				TranslateIconToMessageBoxImage(icon));
		});

		// 显式处理关闭按钮情况
		return result.HasValue && result.Value == MessageBoxResult.Yes;
	}

	/// <summary>
	/// 下载HTML内容并处理超时
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	private async Task<string> DownloadHtmlWithTimeoutAsync(CancellationToken cancellationToken)
	{
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		cts.CancelAfter(_httpClient.Timeout);

		// 添加随机延迟避免被Steam封禁
		await Task.Delay(new Random().Next(500, 1500), cts.Token);

		HttpResponseMessage response = await _httpClient.GetAsync(
			_workshopItemUrl,
			HttpCompletionOption.ResponseContentRead,
			cts.Token
		);

		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsStringAsync(cts.Token);
	}

	/// <summary>
	/// 从HTML中解析版本号
	/// </summary>
	/// <param name="html"></param>
	/// <returns></returns>
	/// <exception cref="Exception"></exception>
	private static string ParseVersionFromHtml(string html)
	{
		var htmlDoc = new HtmlDocument();
		htmlDoc.LoadHtml(html);

		// 查找版本标签
		var versionNodes = htmlDoc.DocumentNode.SelectNodes(
			"//div[contains(@class, 'workshopTags')]//a[contains(@href, 'version:')]"
		);

		if (versionNodes == null || versionNodes.Count == 0)
			throw new Exception("Version tags not found");

		foreach (var node in versionNodes)
		{
			string href = node.GetAttributeValue("href", "");
			var match = RegexHelper.CheckVersionRegex().Match(href);

			if (match.Success)
				return match.Groups[1].Value;
		}

		throw new Exception("No valid version found in tags");
	}

	/// <summary>
	/// 检查在线版本是否比当前版本新
	/// </summary>
	/// <param name="onlineVersion"></param>
	/// <param name="currentVersion"></param>
	/// <returns></returns>
	private static bool IsNewerVersion(string onlineVersion, string currentVersion)
	{
		Version online = NormalizeVersion(onlineVersion);
		Version current = NormalizeVersion(currentVersion);

		return online > current;
	}

	/// <summary>
	/// 规范化版本号
	/// </summary>
	/// <param name="version"></param>
	/// <returns></returns>
	private static Version NormalizeVersion(string version)
	{
		// 规范化版本格式
		int hashIndex = version.IndexOf('+');
		if (hashIndex >= 0)
		{
			version = version[..hashIndex];
		}

		// 规范化版本格式
		var parts = version.Split('.');
		if (parts.Length < 2) version += ".0";
		if (parts.Length < 3) version += ".0";
		if (parts.Length < 4) version += ".0";

		return new Version(version);
	}

	/// <summary>
	/// 在UI线程显示消息框
	/// </summary>
	private static void ShowMessage(string message, string title, MessageBoxIcon icon)
	{
		Application.Current.Dispatcher.Invoke(() =>
		{
			MessageBox.Show(message, title,
				MessageBoxButton.OK,
				TranslateIconToMessageBoxImage(icon));
		});
	}

	/// <summary>
	/// 将MessageBoxIcon转换为MessageBoxImage
	/// </summary>
	/// <param name="icon"></param>
	/// <returns></returns>
	private static MessageBoxImage TranslateIconToMessageBoxImage(MessageBoxIcon icon)
	{
		return icon switch
		{
			MessageBoxIcon.Error => MessageBoxImage.Error,
			MessageBoxIcon.Warning => MessageBoxImage.Warning,
			MessageBoxIcon.Information => MessageBoxImage.Information,
			_ => MessageBoxImage.None
		};
	}
}