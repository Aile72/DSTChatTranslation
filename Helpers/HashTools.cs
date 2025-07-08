using System.Text;
using System.Security.Cryptography;

namespace DSTChatTranslation.Helpers;

public class HashTools
{
	/// <summary>
	/// 计算字符串的哈希值
	/// </summary>
	public static string CalculateHash(string input)
	{
		byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
	}
}
