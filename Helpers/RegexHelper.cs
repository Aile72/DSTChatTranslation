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
	/// 正则表达式筛选玩家昵称
	/// </summary>
	[GeneratedRegex(@"\[Say\] \((.*?)\)(.*?):")]
	private static partial Regex NickNameRegex();

	public static string ProcessNickNameString(string input)
	{
		Match match = NickNameRegex().Match(input);
		return match.Success ? match.Groups[2].Value.Trim() : "";
	}
}
