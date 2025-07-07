using GTranslate;

namespace DSTChatTranslation.Models;

public class LangCodeViewModel
{
	public readonly SortedDictionary<string, string> TargetLanguage = [];

	/// <summary>
	/// LangCodeViewModel构造函数
	/// </summary>
	/// <param name="service"></param>
	public LangCodeViewModel(TranslationServices? service = null)
	{
		foreach (var languagePair in Language.LanguageDictionary)
		{
			// 如果指定了服务，只添加支持该服务的语言
			if (service == null || languagePair.Value.IsServiceSupported(service.Value))
			{
				TargetLanguage.Add(languagePair.Value.ISO6393, languagePair.Value.NativeName);
			}
		}
	}
}