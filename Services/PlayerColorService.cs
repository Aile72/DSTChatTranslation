using System.Diagnostics;
using System.Timers;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Timer = System.Timers.Timer;

namespace DSTChatTranslation.Services;

public class PlayerColorService
{
	private readonly Dictionary<string, (DateTime LastActive, Brush Color)> _playerColors;
	private readonly Timer _cleanupTimer;
	private static readonly Brush[] _availableBrushes;

	static PlayerColorService()
	{
		_availableBrushes =
		[
			// 蓝绿色系
			Brushes.LightSeaGreen,    // 浅海绿
			Brushes.PaleTurquoise,    // 淡青
    
			// 蓝色系
			Brushes.LightSteelBlue,   // 浅钢蓝
			Brushes.PowderBlue,       // 粉末蓝
			Brushes.LightSkyBlue,     // 浅天蓝
    
			// 紫色系
			Brushes.Lavender,         // 薰衣草紫
			Brushes.Thistle,          // 蓟色紫
    
			// 粉色系
			Brushes.LightPink,        // 浅粉
			Brushes.MistyRose,        // 雾玫瑰
			Brushes.LightCoral,       // 浅珊瑚
    
			// 黄色系
			Brushes.PaleGoldenrod,    // 淡金菊黄
			Brushes.LemonChiffon,     // 柠檬绸黄
    
			// 绿色系
			Brushes.LightGreen,       // 浅绿
			Brushes.PaleGreen,        // 淡绿
			Brushes.MintCream,        // 薄荷霜
    
			// 青色系
			Brushes.LightCyan,        // 浅青
			Brushes.Azure,            // 蔚蓝
    
			// 橙色系
			Brushes.LightSalmon,      // 浅鲑鱼
			Brushes.PeachPuff,        // 蜜桃色
    
			// 中性色
			Brushes.LightBlue,        // 浅蓝
			Brushes.Honeydew,         // 蜜露色
		];
	}

	public PlayerColorService()
	{
		_playerColors = [];
		_cleanupTimer = new Timer(30000); // 每30秒检查一次
		_cleanupTimer.Elapsed += CleanupInactivePlayers;
		_cleanupTimer.Start();
	}

	/// <summary>
	/// 获取玩家颜色，如果不存在则分配新颜色
	/// </summary>
	public Brush GetPlayerColor(string playerId)
	{
		// 系统消息使用黄色
		if (playerId == "[System]") return Brushes.Yellow;

		lock (_playerColors)
		{
			// 如果已有记录，更新活动时间并返回颜色
			if (_playerColors.TryGetValue(playerId, out var data))
			{
				_playerColors[playerId] = (DateTime.Now, data.Color);
				return data.Color;
			}

			// 分配新颜色：尝试使用未使用的颜色
			Brush selectedBrush = FindUnusedColor() ?? _availableBrushes[0];

			_playerColors.Add(playerId, (DateTime.Now, selectedBrush));
			return selectedBrush;
		}
	}

	/// <summary>
	/// 查找未使用的颜色
	/// </summary>
	private Brush? FindUnusedColor()
	{
		var usedBrushes = _playerColors.Values.Select(x => x.Color).ToHashSet();
		return _availableBrushes.FirstOrDefault(brush => !usedBrushes.Contains(brush));
	}

	/// <summary>
	/// 清理5分钟未活动的玩家
	/// </summary>
	private void CleanupInactivePlayers(object? sender, ElapsedEventArgs e)
	{
		lock (_playerColors)
		{
			var threshold = DateTime.Now.AddMinutes(-5);
			var inactiveIds = _playerColors
				.Where(kvp => kvp.Value.LastActive < threshold)
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var id in inactiveIds)
			{
				_playerColors.Remove(id);
				Debug.WriteLine($"清理5分钟未活动的玩家: {id}");
			}
		}
	}

	// 在应用程序退出时停止计时器
	public void Stop()
	{
		_cleanupTimer.Enabled = false;
		_cleanupTimer.Stop();
		_cleanupTimer.Dispose();
	}
}