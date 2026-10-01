# 使用说明 / Usage Guide

**中文** ｜ [English](#english)

本文是「一步一步照做就行」的详细说明；想先看功能与原理请回到 [README](README.md)。

---

## 一、Windows

### 1. 准备

1. 解压（或在仓库里进入）`windows/` 目录；
2. 目录里应当有：`67.exe`、`67-Settings.exe`、`build.cmd`、`audio\67.m4a`、`src\`；
3. 不需要安装任何运行库：Win10 / Win11 自带的 .NET Framework 4.x 就够了。

### 2. 先调参数（可选但推荐）

1. 双击 `67-Settings.exe`；
2. 左侧拖动滑杆 / 修改数值：
   - **覆盖屏幕比例**：越大窗口越多越大，默认 75%；
   - **弹窗停留时间**：默认 6 秒（这是「能看到」的时间，觉得短就往上调）；
   - **窗口数量上限**：默认 60；
   - **窗口尺寸倍率**：默认 1.2 倍；
   - **大 67 停留时间**：默认 300 秒，填 `0` = 一直留着直到你手动关；
   - **音量 / 音乐起始位置**：默认 80%、从 0 秒开始；
   - 勾选：全屏特效 / 窗口漂移 / 播放音乐 / 大 67 全屏；
3. 右侧会用真实布局引擎实时预览，底部时间轴显示每个阶段的时长；
4. 点 `保存`（写入 `67.ini`）或 `保存并试一次`（立刻运行）。

### 3. 开演

双击 `67.exe`。流程：延迟 0.3 秒 → 窗口炸出 → 停留 6 秒 → 逐个关闭（音乐继续）→ 全屏巨大 `67`。

### 4. 结束

任意一种都可以，音乐都会立刻停止、进程退出：

- 按 `Esc`；
- 点巨大 `67` 右上角的 `×`；
- 在巨大 `67` 上右键 → `Close (ESC)`；
- 双击巨大 `67`；
- 什么都不做，等 `--final` 设定的时间到（默认 300 秒）。

### 5. 换掉音频 / 静音

- 换音频：把自己的文件放到 `windows\audio\` 并改名为 `67.m4a`，
  或直接 `67.exe --audio "D:\我的音乐\xxx.m4a"`；
- 静音：`67.exe --no-audio`；
- 只想听副歌：`67.exe --audio-start 60`。

### 6. 命令行示例

```bat
67.exe --coverage 0.85 --hold 10                 :: 更满、更久
67.exe --max 40 --window-scale 1.6               :: 少而大的窗口
67.exe --layout grid --seed 7                    :: 规整网格 + 固定布局
67.exe --area 2560x1440                          :: 桌面尺寸识别异常时兜底
67.exe --log run.txt                             :: 输出执行日志，排查问题用
67.exe --dump-layout preview.png                 :: 只导出布局图，不弹窗
```

### 7. 重新编译

双击 `build.cmd`（生成 `67.exe` 与 `67-Settings.exe`）。
脚本每次都会写入新的版本戳，所以生成的 exe 哈希不同 ——
这也正好用来解决「智能应用控制拦截了未签名 exe」的情况。

---

## 二、macOS

```bash
cd linux-mac
chmod +x 67.command 67.py 67.sh      # 首次需要
./67.command                         # 或在访达里双击 67.command
```

- 音频：把文件放到 `linux-mac/67.m4a`，或 `./67.py --audio ~/Music/67.m4a`；
  macOS 用系统自带 `afplay` 播放，无需安装任何东西；
- 参数：`./67.py --hold 8 --coverage 0.85 --volume 0.6`；
- 结束：`Esc`、点击巨大 `67`、或等 `--final` 超时（默认 0 = 一直显示，等你关）。

## 三、Linux

```bash
cd linux-mac
chmod +x 67.sh 67.py
./67.sh
```

- 需要 Python 3 与 tkinter（Debian/Ubuntu：`sudo apt install python3-tk`）；
- 音频播放器按以下顺序自动挑选，装任意一个即可：
  `mpv` → `ffplay`(ffmpeg) → `mplayer` → `mpg123` → `paplay`(PulseAudio)；
  一个都没有也能运行，只是没有声音（会打印提示）；
- Wayland 下若特效窗不显示，用 `--no-fx` 关掉特效层即可，其余流程照常。

---

## 四、常见问题速查

| 现象 | 处理 |
| --- | --- |
| Windows 双击没反应 / 提示被策略阻止 | 双击 `windows/build.cmd` 重新编译一次再运行 |
| 窗口只占屏幕左上角 | 已修复；仍异常时加 `--area 你的分辨率`，并用 `--log` 看日志里的实测值 |
| 没有声音 | 确认音频文件在位（Windows `windows/audio/67.m4a`，Linux/macOS `linux-mac/67.m4a`）；或用了 `--no-audio` |
| 音乐太吵 | 设置工具把音量调低，或 `--volume 0.3` |
| 想快点结束 | `--hold 2 --final 10` |
| 想每次都一样 | `--seed 12345` |
| 只想看布局不弹窗 | `--dump-layout out.png`（Windows 直接出 PNG；Linux/macOS 打印数量与覆盖率） |
| 想彻底删除 | 直接删掉文件夹即可，程序不写注册表、不开机自启（仅 `67.ini` 是设置文件） |

---

<a id="english"></a>

## English

### Windows

1. Open the `windows/` folder - no runtime needed (uses the .NET Framework that ships with Win10/11).
2. Optional: run `67-Settings.exe`, drag the sliders (coverage, hold time, window count, size,
   volume, music start), watch the live preview and the phase timeline, then hit *Save* or
   *Save & try once* (writes `67.ini`).
3. Double click `67.exe`. Delay 0.3 s → burst → 6 s hold → windows close (music keeps playing) →
   full-screen giant `67`.
4. Finish with `Esc`, the giant window's top-right `×`, right click → *Close (ESC)*, a double click,
   or simply wait for the `--final` timeout.

Useful flags:

```bat
67.exe --audio "D:\my music\67.m4a"      :: use your own audio
67.exe --no-audio                        :: stay silent
67.exe --audio-start 60                  :: start 60 s into the track
67.exe --coverage 0.85 --hold 10          :: denser and longer
67.exe --layout grid --seed 7             :: tidy grid, repeatable layout
67.exe --area 2560x1440                   :: wrong desktop size reported by the session
67.exe --log run.txt                      :: write a step by step log
```

Rebuild with `windows/build.cmd` (also the fix if Smart App Control blocks the unsigned exe -
each build gets a fresh version stamp and therefore a fresh file hash).

### macOS / Linux

```bash
cd linux-mac
chmod +x 67.command 67.sh 67.py
./67.command          # macOS (or double click 67.command in Finder)
./67.sh               # Linux
```

Audio: drop your file at `linux-mac/67.m4a` or pass `--audio <file>`. The client picks the first
available player from `afplay` (macOS) → `mpv` → `ffplay` → `mplayer` → `mpg123` → `paplay`;
without any of them it simply runs silently. On Debian/Ubuntu install tkinter with
`sudo apt install python3-tk`. On Wayland, add `--no-fx` if the overlay does not appear.

### Troubleshooting

| Symptom | Fix |
| --- | --- |
| Windows: nothing happens / blocked by policy | run `windows/build.cmd` once, then start `67.exe` again |
| Windows: windows only in the top-left corner | fixed; if it still happens add `--area yourWxyourH` and check `--log` |
| No sound | make sure `67.m4a` exists (or `--audio <file>`), and that you did not pass `--no-audio` |
| Music too loud | lower the volume in the settings app or use `--volume 0.3` |
| Want it shorter | `--hold 2 --final 10` |
| Same layout every time | `--seed 12345` |
| Preview without covering the screen | `--dump-layout out.png` |
| Uninstall | just delete the folder (nothing is written to the registry, no autostart) |

6767676767676767676767676767676767676767
