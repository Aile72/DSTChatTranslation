using System.Text.Json.Serialization;

namespace DSTChatTranslation.Models;

public class AppSettingsModel
{
	public double ScreenX { get; set; } = -1;
	public double ScreenY { get; set; } = -1;
	public double Width { get; set; } = 160;
	public double Height { get; set; } = 200;
	public bool IsWindowLocked { get; set; } = false;
	public double FontSizeMultiple { get; set; } = 0;
	public string TranslationAPI { get; set; } = "Google";
	public string TargetLanguage { get; set; } = "eng";
	public bool UseProxy { get; set; }
	public string? ProxyUrl { get; set; }
	public int AutoCheckVersionFailCount { get; set;} = 0;

	[JsonIgnore]
	public static AppSettingsModel Current { get; set; } = new();
}