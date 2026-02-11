using System;
using System.IO;
using System.Text.Json;

namespace DesktopFabLauncher.Models;

public class AppConfig
{
    /// <summary>右ボタン長押し判定時間 (ms)</summary>
    public int LongPressThresholdMs { get; set; } = 500;

    /// <summary>長押し中の移動許容量 (px)</summary>
    public int MovementThresholdPx { get; set; } = 5;

    /// <summary>Armed状態で無入力時のタイムアウト (ms)（1入力目）</summary>
    public int InputTimeoutMs { get; set; } = 2000;

    /// <summary>2入力目以降の無入力タイムアウト (ms)</summary>
    public int ShortInputTimeoutMs { get; set; } = 700;

    /// <summary>最大入力回数</summary>
    public int MaxInputCount { get; set; } = 5;

    /// <summary>結果表示後にオーバーレイを消すまでの時間 (ms)</summary>
    public int ResultDisplayMs { get; set; } = 600;

    /// <summary>オーバーレイの不透明度 (0.0〜1.0)</summary>
    public double OverlayOpacity { get; set; } = 0.85;

    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        var config = new AppConfig();
        config.Save();
        return config;
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
