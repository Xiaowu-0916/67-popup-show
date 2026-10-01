# 67 (Linux / macOS)

零依赖的 Python 3 + tkinter 客户端，效果与 Windows 版一致：
一堆原生窗口铺满屏幕 → 全屏特效 → 巨大的 `67`，音乐在你关掉大 `67` 时停止。

Dependency-free Python 3 + tkinter client with the same show as the Windows build:
native window burst over the whole screen, full-screen effects, one giant `67`,
and the music stops the moment you close the giant window.

## 运行 / Run

```bash
chmod +x 67.sh 67.command 67.py      # 首次
./67.sh                              # Linux
./67.command                         # macOS（或在访达双击）
python3 67.py --hold 8               # 直接运行
```

## 音频 / Audio

把音频放到本目录 `67.m4a`，或用 `--audio <file>` 指定。
播放器按 `afplay`(macOS) → `mpv` → `ffplay` → `mplayer` → `mpg123` → `paplay` 顺序自动选择。

Drop your audio at `67.m4a` in this folder (or use `--audio <file>`).
Players are picked automatically in the order listed above.

## 参数 / Options

```
--coverage 0.80  --min 14 --max 60   --scale 1.15
--hold 6.0       --final 0（0 = 一直显示直到你关闭）
--audio <file>   --audio-start 0     --volume 0.8    --no-audio
--no-fx          --no-jitter         --seed 12345
--dump-layout out.png                 # 只导出布局信息，不弹窗
```

6767676767676767
