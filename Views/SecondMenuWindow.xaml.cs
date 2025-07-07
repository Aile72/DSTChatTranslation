using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DSTChatTranslation.Helpers;
using DSTChatTranslation.Models;
using DSTChatTranslation.Services;
using GTranslate;
using ComboBox = System.Windows.Controls.ComboBox;

namespace DSTChatTranslation.Views;

public partial class SecondMenuWindow : Window
{
	private readonly MainWindow _mainWindow; // 添加主窗口引用
	private readonly MessageService _messageService; // 消息服务引用
	private bool _isSettingChange = false; // 设置状态标志位

	/// <summary>
	/// 二级窗口入口点
	/// </summary>
	public SecondMenuWindow(MainWindow mainWindow, MessageService messageService)
	{
		_mainWindow = mainWindow; // 保存主窗口引用
		_messageService = messageService; // 保存消息服务

		InitializeComponent();

		// 设置滑动条范围
		outputFontSize_Slider.Minimum = -10;
		outputFontSize_Slider.Maximum = 10;
		outputFontSize_Slider.SmallChange = 0.1;
		outputFontSize_Slider.LargeChange = 1;
		outputFontSize_Slider.IsSnapToTickEnabled = true;

		ShowInTaskbar = false;  // 不要显示在任务栏菜单

		outputFontSize_Slider.ValueChanged += SetFontSize;
		outputTargetTranslationAPI_ComboBox.SelectionChanged += SetTranslationAPI;
		outputTargetLanguage_ComboBox.SelectionChanged += SetTargetLanguage;
		outputProxy_Button.Click += ApplyProxyUrl;

		// 初始化设置
		LoadSettings();
	}

	/// <summary>
	/// 加载设置
	/// </summary>
	private void LoadSettings()
	{
		_isSettingChange = true; // 开始加载设置

		try
		{
			SelectComboBoxItem(outputTargetTranslationAPI_ComboBox, AppSettingsModel.Current.TranslationAPI);
			LoadLanguagesForCurrentAPI();

			double fontSize = Math.Clamp(AppSettingsModel.Current.FontSizeMultiple, -10, 10);
			outputFontSize_Slider.Value = fontSize;
			UpdateFontSizeText(fontSize);

			outputProxy_TextBox.Text = AppSettingsModel.Current.ProxyUrl ?? "";
			outputProxy_CheckBox.IsChecked = AppSettingsModel.Current.UseProxy;
		}
		finally
		{
			_isSettingChange = false; // 结束加载设置
		}
	}

	/// <summary>
	/// 根据当前选择的翻译API加载语言列表
	/// </summary>
	private void LoadLanguagesForCurrentAPI()
	{
		var service = AppSettingsModel.Current.TranslationAPI switch
		{
			"Bing" => TranslationServices.Bing,
			"Azure" => TranslationServices.Microsoft,
			"Yandex" => TranslationServices.Yandex,
			_ => TranslationServices.Google
		};

		var langItems = new LangCodeViewModel(service).TargetLanguage;
		outputTargetLanguage_ComboBox.ItemsSource = langItems;

		// 简化选择逻辑
		string targetLang = AppSettingsModel.Current.TargetLanguage;
		var selectedItem = langItems.FirstOrDefault(x =>
			x.Key.Equals(targetLang, StringComparison.OrdinalIgnoreCase));

		outputTargetLanguage_ComboBox.SelectedItem = selectedItem;
	}

	/// <summary>
	/// 选择ComboBox中的项
	/// </summary>
	private static void SelectComboBoxItem(ComboBox comboBox, string value)
	{
		foreach (var item in comboBox.Items)
		{
			if (item is ComboBoxItem cbi && cbi.Content.ToString() == value)
			{
				comboBox.SelectedItem = cbi;
				return;
			}
		}
	}

	/// <summary>
	/// 更新字体大小文本显示
	/// </summary>
	private void UpdateFontSizeText(double value)
	{
		int percentage = (int)((value + 10) * 10);
		outputFontSize_TextBlock.Text = $"Font Size: {percentage}%";
	}

	/// <summary>
	/// 设置字体大小事件
	/// </summary>
	private void SetFontSize(object? sender, RoutedPropertyChangedEventArgs<double> e)
	{
		double fontSize = Math.Round(e.NewValue, 1);
		UpdateFontSizeText(fontSize);

		AppSettingsModel.Current.FontSizeMultiple = fontSize;
		MainWindow.SaveSettings(); // 保存设置
	}

	/// <summary>
	/// 设置翻译API事件
	/// </summary>
	private void SetTranslationAPI(object? sender, SelectionChangedEventArgs e)
	{
		if (_isSettingChange) return; // 忽略加载期间的变更

		if (outputTargetTranslationAPI_ComboBox.SelectedItem is ComboBoxItem selectedItem)
		{
			string newApi = selectedItem.Content?.ToString() ?? "Google";

			// 只有当API真正改变时才重置语言
			if (newApi != AppSettingsModel.Current.TranslationAPI)
			{
				AppSettingsModel.Current.TranslationAPI = newApi;

				// 重置目标语言为英语（默认值）
				AppSettingsModel.Current.TargetLanguage = "eng";
				MainWindow.SaveSettings(); // 保存设置

				// 重新加载语言列表并选择英语
				LoadLanguagesForCurrentAPI();

				// 清除翻译缓存
				_mainWindow.ResetTranslationService();
			}
		}
	}

	/// <summary>
	/// 设置目标语言事件
	/// </summary>
	private void SetTargetLanguage(object? sender, SelectionChangedEventArgs e)
	{
		if (_isSettingChange) return; // 忽略加载期间的变更

		if (outputTargetLanguage_ComboBox.SelectedItem is KeyValuePair<string, string> selectedItem)
		{
			AppSettingsModel.Current.TargetLanguage = selectedItem.Key;
			MainWindow.SaveSettings(); // 保存设置

			// 清除翻译缓存
			_mainWindow.ResetTranslationService();
		}
	}

	/// <summary>
	/// 应用代理地址
	/// </summary>
	private void ApplyProxyUrl(object? sender, RoutedEventArgs e)
	{
		AppSettingsModel.Current.ProxyUrl = outputProxy_TextBox.Text;
		MainWindow.SaveSettings(); // 保存设置

		// 清除翻译缓存
		_mainWindow.ResetTranslationService();
	}

	/// <summary>
	/// 代理复选框点击事件
	/// </summary>
	private void ProxyCheckBox_Click(object? sender, RoutedEventArgs e)
	{
		if (_isSettingChange) return; // 忽略加载期间的变更

		if (outputProxy_CheckBox.IsChecked.HasValue)
		{
			bool newValue = outputProxy_CheckBox.IsChecked.Value;
			AppSettingsModel.Current.UseProxy = newValue;
			MainWindow.SaveSettings();

			// 使用主窗口引用显示状态消息
			_messageService.AddSystemMessage($"Proxy {(newValue ? "enabled" : "disabled")}");

			// 添加调试输出
			Debug.WriteLine($"代理状态切换: {newValue}");

			// 清除翻译缓存
			_mainWindow.ResetTranslationService();
		}
	}



	/// <summary>
	/// 点击作者名称访问网站
	/// </summary>
	private void OutputAbout_TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		string url = "https://space.bilibili.com/34417845";
		OpenUrlHandler.OpenUrlInDefaultBrowser(url); // 访问网站
	}
}