# 67

**中文** ｜ [English](#english)

一个《67》桌面弹窗狂欢程序：瞬间炸出一大堆原生窗口铺满整块屏幕（每个窗口里都是 `67`），
配上全屏特效和抖音《67》音频，最后留下一个全屏的巨大 `67`；关掉它的那一刻音乐才停。

> Windows 端是编译好的原生 Win32 程序（WinForms/GDI+，非 HTML）；
> Linux / macOS 端是零依赖的 Python 3 + tkinter 客户端，效果一致。

![布局预览](docs/layout.png)

![全屏特效](docs/fx.png)

![巨大 67](docs/finale.png)

## 特性

- **自动适配任何设备**：先声明 per-monitor-v2 DPI 感知，再用 `EnumDisplaySettings`
  交叉核对真实分辨率，必要时还有 `--area` 手动兜底 —— 不管是 1080p、4K 还是 125%/200% 缩放，
  打开就是全屏铺满（旧版本在缩放屏上只占左上角的 bug 已修）。
- **随机但铺满**：窗口在全屏随机撒点（随机大小、随机重叠、允许轻微出屏），
  同时保证 4×4 共 16 个区域每个都至少有一个窗口，不会出现空角或全挤中间。
- **花样足够多**：16 种配色、5 种文字画法（实心/渐变/描边/阴影/霓虹）、
  随机字体、贴纸（星星/闪电/爱心/圆环/闪光）、卡通表情（大小眼/墨镜）、
  彩虹渐变色轮、弹出缩放动画、部分窗口轻微漂移。
- **全屏特效层**：逐像素透明的彩虹放射光、飘落的 `67` 雨、彩纸、扩散光环、霓虹描边；
  用 `SetWindowPos(HWND_TOPMOST)` 压在巨窗之上，鼠标可穿透、不抢焦点。
- **音频跟着巨窗走**：从音频开头播放，小窗关闭时**不停**，你关掉巨大 `67` 的瞬间才停止。
- **可视化设置工具**（Windows）：滑杆调时间/数量/尺寸/音量，右侧用同一套布局引擎实时预览，
  底部时间轴画出「延迟 → 弹出 → 停留 → 关闭 → 大 67」各阶段时长；保存进 `67.ini`。

![设置工具](docs/settings.png)

## 快速开始

### Windows

1. 双击 `windows/67-Settings.exe` 先调好时间/数量/音量（可选）；
2. 双击 `windows/67.exe` 开演；
3. 随时按 `Esc` 全部关闭；大 `67` 可以按 `Esc`、点右上角 `×`、右键菜单或双击关掉。

### macOS

```bash
cd linux-mac
./67.command            # 或在访达里双击 67.command
```

（首次可能需要 `chmod +x 67.command 67.py`；音乐用系统自带的 `afplay` 播放。）

### Linux

```bash
cd linux-mac
./67.sh                 # 或 python3 67.py
```

音乐播放器按 `mpv → ffplay → mplayer → mpg123 → paplay` 顺序自动挑选，
一个都没有时会静音运行并提示。

## 音频（请自备）

仓库**不包含**那首《67》音频（版权原因，请勿随仓库分发）。想听到原版效果，
把自己的音频文件放到对应位置即可，程序会自动加载：

| 平台 | 放置位置 |
| --- | --- |
| Windows | `windows/audio/67.m4a`（或 `67.exe --audio <你的文件>`） |
| Linux / macOS | `linux-mac/67.m4a`（或 `./67.py --audio <你的文件>`） |

没有音频文件时程序照常运行，只是没有声音。

## 常用参数

Windows `67.exe` 与 Linux/macOS `67.py` 参数基本一致：

```
--coverage 0.80    目标覆盖率（Windows 默认 0.75）
--min 14 --max 60  最少/最多窗口数
--hold 6.0         弹窗停留秒数
--final 300        大 67 自动关闭秒数（0 = 一直留着等你关）
--scale 1.2        窗口尺寸倍率
--audio <文件>     指定音频      --no-audio   静音
--no-fx            关掉全屏特效  --layout chaos|grid|scatter  (Windows)
--area 2560x1440   强制桌面尺寸（缩放/远程会话异常时兜底）
--seed 12345       固定随机布局
```

## 重新编译（Windows）

双击 `windows/build.cmd`（需要 Win10/11 自带的 .NET Framework 4.x，含 WPF 程序集），
会同时生成 `67.exe` 与 `67-Settings.exe`。

## 说明

- Windows 上如果被「智能应用控制（Smart App Control）」拦截：它是按文件哈希做云端信誉判定的，
  重新双击 `build.cmd` 生成一份新 exe（构建脚本每次都会写入新的版本戳）再运行即可。
- 程序是纯前端表演：不联网、不写注册表、不驻留、不修改任何文件。
- 弹窗阶段会把屏幕盖住几秒，这是设计效果，`Esc` 随时中止。

## License

MIT

---

## English

**A "67" desktop show.** A burst of native windows fills your whole screen (each one showing `67`),
backed by full-screen effects and the `67` soundtrack, ending with one giant full-screen `67`.
The music keeps playing while the small windows close and only stops when *you* close the giant one.

The Windows build is a compiled native Win32 app (WinForms/GDI+, no HTML, no browser);
the Linux/macOS build is a dependency-free Python 3 + tkinter client with the same behaviour.

**Highlights**

- **Fits every device**: per-monitor-v2 DPI awareness plus an `EnumDisplaySettings` cross-check
  (and an `--area` escape hatch) so 1080p / 4K / 125% / 200% scaling all open truly full screen.
- **Random but complete**: windows are scattered at random positions, sizes and overlaps,
  while a 4x4 region check guarantees that no part of the screen is left empty.
- **Loud by design**: 16 colours, five text styles (solid / gradient / outline / shadow / neon),
  random fonts, stickers, cartoon faces, hue cycling, pop-in animation and drifting windows.
- **Full-screen effect layer**: per-pixel-alpha rainbow rays, falling `67` rain, confetti,
  expanding rings and a neon border, kept click-through above the giant window.
- **Visual settings app** (Windows): sliders with a live preview built on the real layout engine
  and a time line for every phase, saved to `67.ini`.

The original `67` audio is **not** included for copyright reasons - drop your own file at
`windows/audio/67.m4a` (or `linux-mac/67.m4a`) and it will be picked up automatically.

MIT licensed.

67676767676767676767676767676767676767
