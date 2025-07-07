using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using DSTChatTranslation.Helpers;
using DSTChatTranslation.Models;

namespace DSTChatTranslation.Services;

public class TranslationService : IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly Dictionary<string, string> _translatedTexts = [];
	private readonly MessageService _messageService;
	private readonly LRUCache<string, string> _translationCache = new(1000);

	public TranslationService(MessageService messageService)
	{
		_messageService = messageService;

		// 创建可复用的HttpClient
		var handler = new CustomHttpClientHandler
		{
			UseProxy = AppSettingsModel.Current.UseProxy,
			Proxy = !string.IsNullOrEmpty(AppSettingsModel.Current.ProxyUrl)
				? new WebProxy(AppSettingsModel.Current.ProxyUrl)
				: null
		};

		_httpClient = new HttpClient(handler)
		{
			Timeout = TimeSpan.FromSeconds(10)
		};
	}


	/// <summary>
	/// 翻译聊天内容
	/// </summary>
	public async Task<string> TranslateChatAsync(string originalChat, CancellationToken _)
	{
		if (string.IsNullOrWhiteSpace(originalChat) ||
			originalChat.Length < 3 ||
			RegexHelper.AllDigitsRegex().IsMatch(originalChat))
		{
			return originalChat;
		}

		// 检查缓存
		string hash = HashTools.CalculateHash($"{originalChat}-{AppSettingsModel.Current.TargetLanguage}-{AppSettingsModel.Current.TranslationAPI}");
		if (_translationCache.TryGet(hash, out var cached))
		{
			return cached;
		}

		try
		{
			// 获取目标语言 - 默认为英语
			string targetLangCode = !string.IsNullOrEmpty(AppSettingsModel.Current.TargetLanguage)
				? AppSettingsModel.Current.TargetLanguage
				: "eng";

			var language = Language.GetLanguage(targetLangCode);

			// 根据设置选择翻译服务
			ITranslator translator = AppSettingsModel.Current.TranslationAPI switch
			{
				"Bing" when language.IsServiceSupported(TranslationServices.Bing)
					=> new BingTranslator(_httpClient),
				"Azure" when language.IsServiceSupported(TranslationServices.Microsoft)
					=> new MicrosoftTranslator(_httpClient),
				"Yandex" when language.IsServiceSupported(TranslationServices.Yandex)
					=> new YandexTranslator(_httpClient),
				_ => new GoogleTranslator(_httpClient) // 默认使用Google翻译
			};

			// 直接使用语言对象执行翻译
			ITranslationResult result = await translator.TranslateAsync(
				originalChat,
				language,
				null);

			string translated = result.Translation ?? originalChat;

			// 更新缓存
			_translationCache.Add(hash, translated);

			return translated;
		}
		catch (Exception ex) when (
			ex is HttpRequestException ||
			ex is TaskCanceledException)
		{
			string errorMsg = "Translation service unavailable";
			_messageService.AddSystemMessage(errorMsg);
			return originalChat; // 返回原始内容
		}
		catch (Exception)
		{
			string errorMsg = "Translation service error";
			_messageService.AddSystemMessage(errorMsg);
			return originalChat; // 返回原始内容
		}
	}

	/// <summary>
	/// 并行翻译单个消息
	/// </summary>
	public async Task<(string line, string id, string nickName, string chat)> TranslateMessageAsync(
		(string line, string id, string nickName, string chat) item, CancellationToken token)
	{
		string translated = await TranslateChatAsync(item.chat, token);
		return (item.line, item.id, item.nickName, translated);
	}

	/// <summary>
	/// 清除翻译缓存
	/// </summary>
	public void ClearCache()
	{
		_translationCache.Clear();
		Debug.WriteLine("翻译缓存已清除");
	}

	public void Dispose()
	{
		_httpClient?.Dispose();
		GC.SuppressFinalize(this);
	}
}