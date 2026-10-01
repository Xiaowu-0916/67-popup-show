#!/usr/bin/env python3
"""
67 - cross platform desktop show (Linux / macOS client)

Plays the same show as the Windows build: a burst of native windows showing
"67" spread over the whole screen, a full screen effect layer and one giant
"67" window, with the soundtrack playing in the background.

Only the Python standard library is used (tkinter + the OS audio player).

    ./67.py                     run the show
    ./67.py --hold 8            keep the windows up for 8 seconds
    ./67.py --no-audio          stay silent
    ./67.py --audio ~/67.m4a    use your own audio file
    ./67.py --no-fx             skip the full screen effects
    Esc                         closes everything immediately
"""

import argparse
import math
import os
import random
import shutil
import subprocess
import sys
import time

import tkinter as tk

# --------------------------------------------------------------------------- #
# colours / helpers
# --------------------------------------------------------------------------- #

BACKS = [
    (255, 59, 48), (255, 149, 0), (255, 214, 10), (52, 199, 89),
    (0, 199, 190), (48, 176, 199), (10, 132, 255), (94, 92, 230),
    (191, 90, 242), (255, 55, 95), (242, 242, 247), (28, 28, 30),
    (142, 142, 147), (11, 61, 145), (0, 122, 90), (140, 90, 40),
]

FONTS = ["Impact", "Helvetica", "DejaVu Sans", "Liberation Sans",
         "Arial", "Ubuntu", "Noto Sans", "Courier New"]

TITLES = ["67", "67!", "67 67", "6767", "6 7", "67 (1)", "67 (2)",
          "67 (3)", "67 - Copy", "67.exe", "SIXTY SEVEN", "sixty-seven",
          "67 everywhere", "67 is coming", "67 67 67", "happy 67"]


def hexcolour(rgb):
    return "#%02x%02x%02x" % rgb


def luminance(rgb):
    return (0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2]) / 255.0


def pick_ink(back):
    return (12, 14, 20) if luminance(back) > 0.62 else (255, 255, 255)


def hue_colour(h, saturation=0.9, lightness=0.6):
    """h in 0..1 -> rgb tuple."""
    h = h % 1.0
    q = lightness * (1 + saturation) if lightness < 0.5 else lightness + saturation - lightness * saturation
    p = 2 * lightness - q

    def channel(t):
        t = t % 1.0
        if t < 1.0 / 6.0:
            return p + (q - p) * 6 * t
        if t < 1.0 / 2.0:
            return q
        if t < 2.0 / 3.0:
            return p + (q - p) * (2.0 / 3.0 - t) * 6
        return p

    return (int(round(channel(h + 1 / 3.0) * 255)),
            int(round(channel(h) * 255)),
            int(round(channel(h - 1 / 3.0) * 255)))


def parse_args():
    parser = argparse.ArgumentParser(
        description="67 - desktop popup show (Linux / macOS)",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    parser.add_argument("--coverage", type=float, default=0.80,
                        help="share of the screen the burst covers")
    parser.add_argument("--min", type=int, default=14, help="minimum windows")
    parser.add_argument("--max", type=int, default=60, help="maximum windows")
    parser.add_argument("--hold", type=float, default=6.0,
                        help="seconds the burst stays on screen")
    parser.add_argument("--final", type=float, default=0.0,
                        help="seconds the giant window stays (0 = until you close it)")
    parser.add_argument("--delay", type=float, default=0.3,
                        help="seconds before the burst starts")
    parser.add_argument("--scale", type=float, default=1.15,
                        help="window size multiplier")
    parser.add_argument("--audio", default=None, help="audio file to play")
    parser.add_argument("--audio-start", type=float, default=0.0,
                        help="start the audio at this many seconds")
    parser.add_argument("--volume", type=float, default=0.8,
                        help="volume 0..1 (players that support it)")
    parser.add_argument("--no-audio", action="store_true", help="mute")
    parser.add_argument("--no-fx", action="store_true",
                        help="disable the full screen effect layer")
    parser.add_argument("--no-jitter", action="store_true",
                        help="disable the drifting windows")
    parser.add_argument("--seed", type=int, default=0, help="random seed")
    parser.add_argument("--dump-layout", default=None,
                        help="write the computed layout to a PNG and exit "
                             "(needs ImageMagick or Pillow)")
    return parser.parse_args()


# --------------------------------------------------------------------------- #
# audio
# --------------------------------------------------------------------------- #


class Soundtrack:
    """Starts whichever audio player the system provides and stops it again."""

    def __init__(self, path, start, volume, enabled):
        self.path = path
        self.start = max(0.0, start)
        self.volume = min(1.0, max(0.0, volume))
        self.enabled = enabled
        self.process = None

    def candidates(self):
        vol = str(int(self.volume * 100))
        seek = str(self.start)
        return [
            ("afplay", ["afplay", "-v", str(self.volume)]),           # macOS
            ("mpv", ["mpv", "--no-video", "--really-quiet",
                     "--volume=" + vol]),
            ("ffplay", ["ffplay", "-nodisp", "-autoexit",
                        "-loglevel", "quiet",
                        "-volume", vol, "-ss", seek]),
            ("mplayer", ["mplayer", "-really-quiet", "-novideo",
                         "-volume", vol, "-ss", seek]),
            ("mpg123", ["mpg123", "-q", "-f", str(int(self.volume * 32768))]),
            ("paplay", ["paplay"]),                                   # Linux
        ]

    def start_playback(self):
        if not self.enabled or not self.path or not os.path.isfile(self.path):
            return False
        for name, command in self.candidates():
            if shutil.which(name) is None:
                continue
            try:
                self.process = subprocess.Popen(
                    command + [self.path],
                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                    start_new_session=True)
                print("audio: playing with %s" % name)
                return True
            except Exception as exc:                      # pragma: no cover
                print("audio: %s failed (%s)" % (name, exc))
        print("audio: no supported player found, running without sound")
        return False

    def stop(self):
        if self.process is None:
            return
        try:
            if os.name == "nt":                            # pragma: no cover
                self.process.terminate()
            else:
                os.killpg(os.getpgid(self.process.pid), 15)
        except Exception:
            try:
                self.process.terminate()
            except Exception:
                pass
        self.process = None
        print("audio: stopped")


# --------------------------------------------------------------------------- #
# the show
# --------------------------------------------------------------------------- #


class Show:
    def __init__(self, options):
        self.opt = options
        self.rng = random.Random(options.seed or None)
        self.root = tk.Tk()
        self.root.withdraw()
        self.screen_w = self.root.winfo_screenwidth()
        self.screen_h = self.root.winfo_screenheight()
        self.windows = []
        self.fx = None
        self.giant = None
        self.sound = None
        self.finished = False
        self.phase = "burst"

    # -- geometry ---------------------------------------------------------- #

    def random_window_rect(self):
        roll = self.rng.random()
        if roll < 0.30:
            area = 0.060 + self.rng.random() * 0.055
        elif roll < 0.72:
            area = 0.028 + self.rng.random() * 0.022
        else:
            area = 0.012 + self.rng.random() * 0.012
        area *= self.opt.scale * (self.opt.coverage / 0.80)

        aspect = 1.10 + self.rng.random() * 1.50
        width = math.sqrt(area * aspect) * self.screen_w
        height = area / max(0.02, math.sqrt(area * aspect)) * self.screen_h
        width = max(120, min(width, self.screen_w * 0.75))
        height = max(80, min(height, self.screen_h * 0.70))

        x = self.rng.randint(0, max(1, self.screen_w - int(width)))
        y = self.rng.randint(0, max(1, self.screen_h - int(height)))
        return int(x), int(y), int(width), int(height)

    def layout_windows(self):
        goal = max(self.opt.min, min(self.opt.max,
                                     int(round(self.opt.coverage * 26))))
        rects = [self.random_window_rect() for _ in range(goal)]

        # make sure every one of the 4x4 screen areas holds a window
        regions = 4
        filled = [[False] * regions for _ in range(regions)]
        for x, y, w, h in rects:
            rx = min(regions - 1, max(0, int((x + w / 2) * regions / self.screen_w)))
            ry = min(regions - 1, max(0, int((y + h / 2) * regions / self.screen_h)))
            filled[rx][ry] = True

        for ry in range(regions):
            for rx in range(regions):
                if filled[rx][ry] or len(rects) >= self.opt.max:
                    continue
                x, y, w, h = self.random_window_rect()
                cx = int((rx + 0.5) * self.screen_w / regions)
                cy = int((ry + 0.5) * self.screen_h / regions)
                rects.append((cx - w // 2, cy - h // 2, w, h))
                filled[rx][ry] = True
        return rects

    # -- building the windows ---------------------------------------------- #

    def spawn_windows(self, rects):
        for index, (x, y, w, h) in enumerate(rects):
            back = self.rng.choice(BACKS)
            ink = pick_ink(back)
            top = tk.Toplevel(self.root)
            top.overrideredirect(True)
            top.geometry("%dx%d+%d+%d" % (w, h, x, y))
            top.configure(bg=hexcolour(back))
            try:
                top.attributes("-topmost", True)
            except tk.TclError:
                pass

            canvas = tk.Canvas(top, width=w, height=h, highlightthickness=0,
                               bd=0, bg=hexcolour(back))
            canvas.pack(fill="both", expand=True)

            title_h = max(10, int(h * 0.14))
            canvas.create_rectangle(0, 0, w, title_h,
                                    fill=hexcolour(tuple(max(0, c - 60) for c in back)),
                                    outline="")
            canvas.create_text(6, title_h / 2, anchor="w",
                               text=self.rng.choice(TITLES),
                               fill=hexcolour(ink),
                               font=(self.rng.choice(FONTS), max(7, int(h * 0.055))))

            size = int(min(h * 0.62, w * 0.62))
            canvas.create_text(w / 2, (h + title_h) / 2, text="67",
                               fill=hexcolour(ink),
                               font=(self.rng.choice(FONTS), max(16, size),
                                     "bold"))

            # a couple of stickers for the loud look
            for _ in range(self.rng.randint(2, 5)):
                sx = self.rng.randint(int(w * 0.06), max(1, int(w * 0.94)))
                sy = self.rng.randint(int(title_h + h * 0.05), max(1, int(h * 0.94)))
                r = self.rng.randint(max(4, int(min(w, h) * 0.05)),
                                     max(6, int(min(w, h) * 0.16)))
                colour = hexcolour(hue_colour(self.rng.random()))
                kind = self.rng.randint(0, 2)
                if kind == 0:
                    canvas.create_polygon(sx, sy - r, sx + r * 0.3, sy - r * 0.3,
                                          sx + r, sy, sx + r * 0.3, sy + r * 0.3,
                                          sx, sy + r, sx - r * 0.3, sy + r * 0.3,
                                          sx - r, sy, sx - r * 0.3, sy - r * 0.3,
                                          fill=colour, outline="")
                elif kind == 1:
                    canvas.create_oval(sx - r, sy - r, sx + r, sy + r,
                                       outline=colour, width=max(2, r // 5))
                else:
                    canvas.create_line(sx - r, sy + r, sx, sy - r, sx + r, sy + r,
                                       fill=colour, width=max(2, r // 4))

            top.bind("<Escape>", lambda _e: self.close_all())
            top.bind("<Button-3>", lambda _e, t=top: t.destroy())
            top.bind("<Key>", lambda e: self.close_all() if e.keysym == "Escape" else None)
            self.windows.append((top, x, y))

    # -- effects ----------------------------------------------------------- #

    def start_fx(self):
        if self.opt.no_fx:
            return
        fx = tk.Toplevel(self.root)
        fx.overrideredirect(True)
        fx.geometry("%dx%d+0+0" % (self.screen_w, self.screen_h))
        fx.configure(bg="black")
        try:
            fx.attributes("-topmost", True)
            fx.attributes("-alpha", 0.90)
        except tk.TclError:
            pass
        canvas = tk.Canvas(fx, width=self.screen_w, height=self.screen_h,
                           highlightthickness=0, bd=0, bg="black")
        canvas.pack()
        self.fx = (fx, canvas)

    def draw_fx(self):
        if self.fx is None:
            return
        fx, canvas = self.fx
        canvas.delete("all")
        cx, cy = self.screen_w / 2, self.screen_h / 2
        radius = math.hypot(cx, cy)

        spin = time.time() * 25.0
        wedges = 18
        for i in range(wedges):
            a0 = math.radians((spin + i * 360.0 / wedges) % 360.0)
            a1 = a0 + math.radians(360.0 / wedges * 0.55)
            colour = hexcolour(hue_colour((i / wedges + spin / 900.0) % 1.0))
            canvas.create_polygon(cx, cy,
                                  cx + math.cos(a0) * radius, cy + math.sin(a0) * radius,
                                  cx + math.cos(a1) * radius, cy + math.sin(a1) * radius,
                                  fill=colour, outline="")

        for i in range(26):
            x = self.rng.randint(0, self.screen_w)
            y = self.rng.randint(0, self.screen_h)
            canvas.create_text(x, y, text="67",
                               fill=hexcolour(hue_colour(self.rng.random())),
                               font=(self.rng.choice(FONTS),
                                     self.rng.randint(14, 40), "bold"))

    def stop_fx(self):
        if self.fx is not None:
            try:
                self.fx[0].destroy()
            except tk.TclError:
                pass
            self.fx = None

    # -- the timeline ------------------------------------------------------ #

    def start(self):
        interval = int(max(8, 1000 / max(1, self.opt.max)))
        self.burst_timer = None
        self.burst_queue = self.layout_windows()
        self.after_delay(self.opt.delay, self.begin)

    def after_delay(self, seconds, callback):
        self.root.after(int(seconds * 1000), callback)

    def begin(self):
        print("burst: %d windows" % len(self.burst_queue))
        self.start_fx()
        if not self.opt.no_audio:
            self.sound = Soundtrack(self.opt.audio, self.opt.audio_start,
                                    self.opt.volume, True)
            self.sound.start_playback()
        self.pump_burst()
        self.root.after(int(self.opt.hold * 1000), self.end_burst)

    def pump_burst(self):
        for _ in range(3):
            if not self.burst_queue:
                break
            self.spawn_windows([self.burst_queue.pop(0)])
        self.draw_fx()
        if self.burst_queue:
            self.root.after(12, self.pump_burst)
        else:
            print("all windows visible")

    def end_burst(self):
        print("closing windows (music keeps playing)")
        self.stop_fx()
        for index, (top, _x, _y) in enumerate(self.windows):
            delay = int(index * 35)
            self.root.after(delay, lambda t=top: self.safe_destroy(t))
        self.root.after(len(self.windows) * 35 + 250, self.show_giant)

    def safe_destroy(self, widget):
        try:
            widget.destroy()
        except tk.TclError:
            pass

    def show_giant(self):
        self.windows = []
        if self.sound is not None:
            print("audio: still playing until the giant window is closed")

        top = tk.Toplevel(self.root)
        top.overrideredirect(True)
        top.geometry("%dx%d+0+0" % (self.screen_w, self.screen_h))
        top.configure(bg="#0b0e14")
        try:
            top.attributes("-topmost", True)
        except tk.TclError:
            pass

        canvas = tk.Canvas(top, width=self.screen_w, height=self.screen_h,
                           highlightthickness=0, bd=0, bg="#0b0e14")
        canvas.pack()
        size = int(min(self.screen_h * 0.78, self.screen_w * 0.58))
        canvas.create_text(self.screen_w / 2, self.screen_h / 2, text="67",
                           fill="#7ef0d0",
                           font=(self.rng.choice(FONTS), size, "bold"))
        canvas.create_text(self.screen_w - 60, 50, text="\u00d7", fill="#cfd6e4",
                           font=(self.rng.choice(FONTS), 22, "bold"))

        top.bind("<Escape>", lambda _e: self.close_all())
        top.bind("<Key>", lambda e: self.close_all() if e.keysym == "Escape" else None)
        top.bind("<Button-1>", lambda _e: self.close_all())
        top.focus_force()
        self.giant = top
        print("giant 67 window shown - press Esc or click it to finish")

        if self.opt.final > 0:
            self.root.after(int(self.opt.final * 1000), self.close_all)

    def close_all(self):
        if self.finished:
            return
        self.finished = True
        if self.sound is not None:
            self.sound.stop()
        for top, _x, _y in self.windows:
            self.safe_destroy(top)
        if self.giant is not None:
            self.safe_destroy(self.giant)
        self.stop_fx()
        try:
            self.root.quit()
        except tk.TclError:
            pass

    def run(self):
        self.start()
        self.root.mainloop()
        return 0


# --------------------------------------------------------------------------- #
# optional layout dump (used to preview without covering the screen)
# --------------------------------------------------------------------------- #


def dump_layout(options, path):
    screen_w = screen_h = None
    try:
        root = tk.Tk()
        root.withdraw()
        screen_w = root.winfo_screenwidth()
        screen_h = root.winfo_screenheight()
        root.destroy()
    except tk.TclError:                                    # headless
        screen_w, screen_h = 1920, 1080

    show = Show.__new__(Show)
    show.opt = options
    show.rng = random.Random(options.seed or None)
    show.screen_w = screen_w
    show.screen_h = screen_h
    rects = show.layout_windows()

    cell = 4
    cols = max(1, (screen_w + cell - 1) // cell)
    rows = max(1, (screen_h + cell - 1) // cell)
    covered = [0] * (cols * rows)
    for x, y, w, h in rects:
        x0 = max(0, x) // cell
        x1 = min(screen_w - 1, x + w - 1) // cell
        y0 = max(0, y) // cell
        y1 = min(screen_h - 1, y + h - 1) // cell
        for cy in range(y0, y1 + 1):
            base = cy * cols
            for cx in range(x0, x1 + 1):
                covered[base + cx] = 1
    ratio = sum(covered) / float(max(1, cols * rows))

    print("windows=%d coverage=%.2f%% screen=%dx%d" %
          (len(rects), ratio * 100.0, screen_w, screen_h))

    convert = shutil.which("convert")
    if convert and "system32" in convert.lower():
        convert = None                      # that is the Windows disk tool
    if convert:
        svg = ['<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d">'
               % (screen_w, screen_h),
               '<rect width="100%" height="100%" fill="#0e1016"/>']
        for index, (x, y, w, h) in enumerate(rects):
            colour = hexcolour(BACKS[index % len(BACKS)])
            svg.append('<rect x="%d" y="%d" width="%d" height="%d" fill="%s" '
                       'stroke="#ffffff" stroke-width="2"/>'
                       % (x, y, w, h, colour))
            svg.append('<text x="%d" y="%d" fill="#ffffff" font-size="%d" '
                       'font-family="Helvetica" font-weight="bold">67</text>'
                       % (x + w // 3, y + int(h * 0.7), max(12, int(h * 0.5))))
        svg.append("</svg>")
        tmp = os.path.splitext(path)[0] + ".svg"
        with open(tmp, "w", encoding="utf-8") as handle:
            handle.write("\n".join(svg))
        subprocess.run([convert, tmp, path], check=False)
        print("wrote %s" % path)
    else:
        print("install ImageMagick to write a PNG preview (skipped)")
    return 0


def main():
    options = parse_args()
    if options.dump_layout:
        return dump_layout(options, options.dump_layout)
    return Show(options).run()


if __name__ == "__main__":
    sys.exit(main())
