using System.Text.RegularExpressions;

namespace DSTChatTranslation.Helpers;

public static partial class RegexHelper
{
	/// <summary>
	/// 正则表达式筛选检查版本号
	/// </summary>
	/// <returns></returns>
	[GeneratedRegex(@"version:([\d\.]+)")]
	public static partial Regex CheckVersionRegex();

	/// <summary>
	/// 正则表达式筛选全数字聊天内容
	/// </summary>
	/// <returns></returns>
	[GeneratedRegex(@"^\d+$")]
	public static partial Regex AllDigitsRegex();
	/// <summary>
	/// 正则表达式筛选昵称和Id
	/// </summary>
	/// <returns></returns>
	[GeneratedRegex(@"\[(?:Say|Whisper)\] \((.*?)\) (.*?):")]
	public static partial Regex NickNameAndIdRegex();

	public static (string id, string name) ProcessIdAndName(string input)
	{
		Match match = NickNameAndIdRegex().Match(input);
		return match.Success
			? (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim())
			: ("", "");
	}
}
