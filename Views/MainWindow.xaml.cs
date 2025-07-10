using DSTChatTranslation.Helpers;
using DSTChatTranslation.Models;
using DSTChatTranslation.Services;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Window = System.Windows.Window;
using Point = System.Windows.Point;


namespace DSTChatTranslation.Views;

public partial class MainWindow : Window
{
	public static MainWindow? Instance { get; private set; }
	public bool IsClosed { get; private set; }

	// 托盘图标
	private NotifyIcon? notifyIcon;

	// 互斥体判断
	private static Mutex? _appMutex;

	// 窗口拖拽相关变量
	private bool isDragging;    // 是否正在鼠标拖拽窗口
	private Point mouseOffset;  // 鼠标拖拽坐标偏移量
	private bool isResizing;    // 是否正在鼠标右下角缩放窗口
	private Point resizeOffset; // 鼠标缩放坐标偏移量
	private Point _lastSavedPosition;

	// 文件路径
	private readonly string filePath;

	// 读取文件相关
	private readonly object _fileLock = new(); // 文件访问锁

	// 定时器和取消令牌
	private DispatcherTimer? _saveTimer = new();
	public static CancellationTokenSource? cancellationTokenSource;

	// 二级菜单窗口
	public SecondMenuWindow secondMenuWindow = null!;
	private bool isSecondMenuWindowOpen = false;

	// 锁定窗口菜单项
	private ToolStripMenuItem lockWindowMenuItem = null!;

	// 添加服务实例
	private readonly PlayerColorService _playerColorService;
	private readonly WindowStateService _windowStateService;
	private readonly MessageService _messageService;
	private readonly FileMonitorService _fileMonitorService;
	private CheckVersionService _checkVersionService = null!;
	public TranslationService translationService;
	public readonly UpdateService updateService;

	public MainWindow()
	{
		Instance = this;

		// 互斥体判断，确保只运行一个实例
		_appMutex = new Mutex(true, "DSTChatTranslation", out bool isNotRunning);
		if (!isNotRunning)
		{
			SystemSounds.Exclamation.Play();
			MessageBox.Show("DSTChatTranslation is already running!");
			Application.Current.Shutdown();
		}

		InitializeComponent();

		// 加载配置
		LoadAppConfig(this);

		// 初始化托盘图标
		InitNotifyIcon();

		// 窗口设置
		Topmost = true; // 置顶窗口
		ShowInTaskbar = false; // 不在任务栏显示图标

		// 事件订阅
		MouseLeftButtonDown += Window_MouseLeftButtonDown;
		MouseLeftButtonUp += Window_MouseLeftButtonUp;
		MouseMove += Window_MouseMove;
		SizeChanged += Window_SizeChanged;
		Loaded += MainWindow_Loaded;
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

		// 获取聊天日志文件路径
		var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		var kleiPath = Path.Combine(documentsPath, "Klei", "DoNotStarveTogether");
		filePath = Path.Combine(kleiPath, "client_chat_log.txt");

		// 初始化服务
		_playerColorService = new PlayerColorService();
		_messageService = new MessageService(this, _playerColorService);
		_fileMonitorService = new FileMonitorService(filePath, _messageService);
		translationService = new TranslationService(_messageService);
		updateService = new UpdateService(this, _fileMonitorService, _messageService);
		_windowStateService = new WindowStateService(this);

		cancellationTokenSource = new CancellationTokenSource();

		// 初始化保存定时器
		_saveTimer.Interval = TimeSpan.FromSeconds(1);
		_saveTimer.Tick += (s, e) =>
		{
			SaveSettings();
			_saveTimer.Stop();
		};
	}

	/// <summary>
	/// 重置翻译服务
	/// </summary>
	public void ResetTranslationService()
	{
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
		cancellationTokenSource = new CancellationTokenSource();

		translationService.ClearCache();
		translationService = new TranslationService(_messageService);
		Debug.WriteLine("翻译服务已重置");
	}

	/// <summary>
	/// 配置文件路径
	/// </summary>
	private static readonly string SettingsFilePath = Path.Combine(
		AppDomain.CurrentDomain.BaseDirectory,
		"settings.json");

	/// <summary>
	/// 加载用户配置
	/// </summary>
	private static void LoadAppConfig(Window window)
	{
		try
		{
			if (File.Exists(SettingsFilePath))
			{
				var json = File.ReadAllText(SettingsFilePath);

				// 反序列化前先检查无效属性
				var jsonNode = JsonNode.Parse(json);
				if (jsonNode is JsonObject jsonObject)
				{
					// 获取当前有效的属性名称
					var validProperties = typeof(AppSettingsModel).GetProperties()
						.Select(p => p.Name)
						.ToHashSet(StringComparer.OrdinalIgnoreCase);

					// 标记需要删除的无效属性
					var invalidProperties = jsonObject
						.Where(kvp => !validProperties.Contains(kvp.Key))
						.Select(kvp => kvp.Key)
						.ToList();

					// 删除无效属性
					foreach (var prop in invalidProperties)
					{
						jsonObject.Remove(prop);
					}

					// 如果有无效属性被删除，重新序列化JSON
					if (invalidProperties.Count > 0)
					{
						json = jsonObject.ToJsonString(CachedJsonSerializerOptions);
						SaveSettings();
						Debug.WriteLine($"移除无效配置项: {string.Join(", ", invalidProperties)}");
					}
				}

				// 反序列化处理后的JSON
				AppSettingsModel.Current = JsonSerializer.Deserialize<AppSettingsModel>(json) ?? new AppSettingsModel();
			}
			else
			{
				AppSettingsModel.Current = new AppSettingsModel();
			}
		}
		catch
		{
			AppSettingsModel.Current = new AppSettingsModel();
		}

		// 应用配置到窗口
		double left = AppSettingsModel.Current.ScreenX == -1 ? window.Left : AppSettingsModel.Current.ScreenX;
		double top = AppSettingsModel.Current.ScreenY == -1 ? window.Top : AppSettingsModel.Current.ScreenY;
		double width = AppSettingsModel.Current.Width > 0 ? AppSettingsModel.Current.Width : window.Width;
		double height = AppSettingsModel.Current.Height > 0 ? AppSettingsModel.Current.Height : window.Height;

		if (AppSettingsModel.Current.ScreenX == -1 ||
		AppSettingsModel.Current.ScreenY == -1)
		{
			left = SystemParameters.WorkArea.Right - width - 10;
			top = 100;
		}

		// 获取当前屏幕工作区
		var workArea = SystemParameters.WorkArea;

		// 修正宽高
		width = Math.Min(width, workArea.Width);
		height = Math.Min(height, workArea.Height);

		// 修正位置
		left = Math.Max(workArea.Left, Math.Min(left, workArea.Right - width));
		top = Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - height));

		window.Left = left;
		window.Top = top;
		window.Width = width;
		window.Height = height;
	}

	// JSON序列化选项
	private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new() { WriteIndented = true };

	/// <summary>  
	/// 保存用户配置  
	/// </summary>  
	public static void SaveSettings()
	{
		var dir = Path.GetDirectoryName(SettingsFilePath);
		if (!Directory.Exists(dir))
			Directory.CreateDirectory(dir!);

		var json = JsonSerializer.Serialize(AppSettingsModel.Current, CachedJsonSerializerOptions);
		File.WriteAllText(SettingsFilePath, json);
	}

	/// <summary>
	/// 显示设置更改事件处理程序
	/// </summary>
	private void OnDisplaySettingsChanged(object? sender, EventArgs e)
	{
		SaveSettings();
	}

	/// <summary>
	/// 初始化托盘图标
	/// </summary>
	private void InitNotifyIcon()
	{
		notifyIcon = new NotifyIcon()
		{
			Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath),
			Text = "DST Chat Translation",
			Visible = true
		};

		ContextMenuStrip contextMenu = new();

		// 二级菜单项
		ToolStripMenuItem secondMenu_MenuItem = new("Settings");
		secondMenu_MenuItem.Click += NotifyIcon_SecondMenu_Click;
		contextMenu.Items.Add(secondMenu_MenuItem);

		// 锁定窗口菜单项
		lockWindowMenuItem = new ToolStripMenuItem("Lock Window")
		{
			Checked = AppSettingsModel.Current.IsWindowLocked
		};
		lockWindowMenuItem.Click += NotifyIcon_LockWindow_Click;
		contextMenu.Items.Add(lockWindowMenuItem);

		// 检查更新菜单项
		ToolStripMenuItem checkUpdate_MenuItem = new("Check Updates");
		checkUpdate_MenuItem.Click += NotifyIcon_CheckUpdate_Click;
		contextMenu.Items.Add(checkUpdate_MenuItem);

		// 退出程序菜单项
		ToolStripMenuItem quit_MenuItem = new("Quit");
		quit_MenuItem.Click += NotifyIcon_Quit_Click;
		contextMenu.Items.Add(quit_MenuItem);

		notifyIcon.ContextMenuStrip = contextMenu;
	}

	/// <summary>
	/// 托盘程序-二级菜单鼠标事件
	/// </summary>
	private void NotifyIcon_SecondMenu_Click(object? sender, EventArgs e)
	{
		if (!isSecondMenuWindowOpen)
		{
			secondMenuWindow = new SecondMenuWindow(this, _messageService);
			secondMenuWindow.Closed += (s, args) => { isSecondMenuWindowOpen = false; };
			secondMenuWindow.Show();
			isSecondMenuWindowOpen = true;
		}
		else
		{
			secondMenuWindow.Activate();
		}
	}

	/// <summary>
	/// 托盘程序-锁定窗口鼠标事件
	/// </summary>
	private void NotifyIcon_LockWindow_Click(object? sender, EventArgs e)
	{
		// 切换锁定状态
		AppSettingsModel.Current.IsWindowLocked = !AppSettingsModel.Current.IsWindowLocked;

		// 应用新状态
		_windowStateService.ApplyWindowLockState(AppSettingsModel.Current.IsWindowLocked);

		// 更新菜单项状态
		lockWindowMenuItem.Checked = AppSettingsModel.Current.IsWindowLocked;

		// 保存设置
		SaveSettings();
	}

	/// <summary>
	/// 托盘程序-检查更新鼠标事件
	/// </summary>
	private async void NotifyIcon_CheckUpdate_Click(object? sender, EventArgs e)
	{
		if (sender is ToolStripMenuItem menuItem)
		{
			try
			{
				menuItem.Enabled = false;
				await _checkVersionService.CheckForUpdatesAsync(isManualCheck: true);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"检查更新时出错: {ex.Message}", "错误",
					MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				// 重新启用菜单项
				menuItem.Enabled = true;
			}
		}
	}

	/// <summary>
	/// 托盘程序-退出程序鼠标事件
	/// </summary>
	private void NotifyIcon_Quit_Click(object? sender, EventArgs e)
	{
		if (notifyIcon == null) return;

		// 先取消所有操作
		if (cancellationTokenSource != null)
		{
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
			cancellationTokenSource = null; // 设置为 null 防止后续访问
		}

		// 关闭应用程序
		Application.Current.Shutdown();
	}

	/// <summary>
	/// 更新确认事件处理 - 打开 Steam 创意工坊页面
	/// </summary>
	private void OnUpdateConfirmed(object? sender, EventArgs e)
	{
		if (sender is CheckVersionService service)
		{
			OpenUrlHandler.OpenUrlInDefaultBrowser(service.WorkshopItemUrl);
		}
	}

	/// <summary>
	/// 加载完成事件处理
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
	{
		// 应用窗口锁定状态
		_windowStateService.ApplyWindowLockState(AppSettingsModel.Current.IsWindowLocked);

		// 初始化版本检查服务
		_checkVersionService = new CheckVersionService(
			workshopItemUrl: "https://steamcommunity.com/sharedfiles/filedetails/?id=3516450074",
			currentVersion: GetCurrentVersion.GetVersion(),
			syncContext: SynchronizationContext.Current
		);
		_checkVersionService.UpdateConfirmed += OnUpdateConfirmed;
		Task.Run(async () =>
		{
			// 启动后立即检查版本
			await _checkVersionService.CheckForUpdatesAsync(isManualCheck: false);
		});
	}


	/// <summary>
	/// 窗口大小改变事件
	/// </summary>
	private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		outputTextBlock.Width = ActualWidth;
	}

	/// <summary>
	/// 窗口鼠标左键按下事件
	/// </summary>
	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		Point mousePosition = e.GetPosition(this);

		// 右下角缩放区域
		if (mousePosition.X > ActualWidth - 10 && mousePosition.Y > ActualHeight - 10)
		{
			isResizing = true;
			resizeOffset = mousePosition;
		}
		// 其他区域拖拽
		else
		{
			isDragging = true;
			mouseOffset = e.GetPosition(this);
		}

		CaptureMouse();
	}

	/// <summary>
	/// 窗口鼠标左键抬起事件
	/// </summary>
	private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		isDragging = false;
		isResizing = false;
		ReleaseMouseCapture();
	}

	/// <summary>
	/// 窗口鼠标移动事件
	/// </summary>
	private void Window_MouseMove(object sender, MouseEventArgs e)
	{
		Point mousePosition = e.GetPosition(this);

		// 设置光标样式
		Cursor = (mousePosition.X > ActualWidth - 10 && mousePosition.Y > ActualHeight - 10)
			? Cursors.SizeNWSE
			: Cursors.Arrow;

		// 处理拖拽
		if (isDragging)
		{
			double deltaX = mousePosition.X - mouseOffset.X;
			double deltaY = mousePosition.Y - mouseOffset.Y;

			Left += deltaX;
			Top += deltaY;
		}
		// 处理缩放
		else if (isResizing)
		{
			Point resizePosition = Mouse.GetPosition(this);
			double widthRatio = resizePosition.X / ActualWidth;
			double heightRatio = resizePosition.Y / ActualHeight;

			double newWidth = Math.Max(MinWidth, ActualWidth * widthRatio);
			double newHeight = Math.Max(MinHeight, ActualHeight * heightRatio);

			Width = newWidth;
			Height = newHeight;
		}

		// 保存窗口位置和大小
		const double saveThreshold = 5; // 5像素变化阈值

		bool needsSave =
		Math.Abs(Left - _lastSavedPosition.X) > saveThreshold ||
		Math.Abs(Top - _lastSavedPosition.Y) > saveThreshold ||
		Math.Abs(Width - AppSettingsModel.Current.Width) > saveThreshold ||
		Math.Abs(Height - AppSettingsModel.Current.Height) > saveThreshold;

		if (needsSave)
		{
			AppSettingsModel.Current.ScreenX = Left;
			AppSettingsModel.Current.ScreenY = Top;
			AppSettingsModel.Current.Width = Width;
			AppSettingsModel.Current.Height = Height;

			_lastSavedPosition = new Point(Left, Top);
			_saveTimer?.Stop();
			_saveTimer?.Start();
		}
	}

	/// <summary>
	/// 窗口关闭事件处理
	/// </summary>
	protected override void OnClosed(EventArgs e)
	{
		IsClosed = true;

		_saveTimer?.Stop();
		_saveTimer = null;

		notifyIcon?.Dispose();

		_playerColorService?.Stop();
		_fileMonitorService?.Dispose();
		translationService?.Dispose();

		if (cancellationTokenSource != null)
		{
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
			cancellationTokenSource = null;
		}

		SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

		Instance = null;

		_appMutex?.Dispose();
		_appMutex = null;

		base.OnClosed(e);
	}
}	