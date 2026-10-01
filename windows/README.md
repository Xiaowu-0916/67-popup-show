# 67 (Windows) / 67 视窗版

双击 `67.exe` 直接开演；双击 `67-Settings.exe` 打开可视化设置（保存到 `67.ini`）。

Double click `67.exe` to run the show, or `67-Settings.exe` for the visual settings app
(saved to `67.ini`).

## 音频 / Audio

把音频放到 `audio/67.m4a`（或 `67.exe --audio <file>`）。
仓库不含音频文件（版权原因）。

Drop your own audio file at `audio/67.m4a` (or pass `--audio <file>`).
The audio is not shipped for copyright reasons.

## 重新编译 / Build

双击 `build.cmd`，需要 Win10/11 自带的 .NET Framework 4.x（含 WPF 程序集）。
会生成 `67.exe` 与 `67-Settings.exe`。

Run `build.cmd` (needs the .NET Framework 4.x that ships with Win10/11, WPF included).

## 参数 / Options

```
--coverage 0.75   目标覆盖率        --min 10 --max 60  窗口数量范围
--hold 6.0        弹窗停留秒数      --final 300        大 67 自动关闭秒数（0=不自动关）
--window-scale 1.2 窗口尺寸倍率     --layout scatter|grid|chaos  布局风格
--volume 0.8      音量             --audio-start 0    音频起始秒数
--no-audio        静音             --no-fx            关掉全屏特效
--no-dpi          关闭 DPI 感知     --area 2560x1440   强制桌面尺寸
--seed 12345      固定布局          --config <file>    指定配置文件
```

6767676767676767
