# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-10-20

### 新增
- 信息面板新增 **当令要义**：一句话说明当令经络的核心养生思路，例如“胃主受纳，早餐最易消化吸收”。
- 宜忌分为三组：**宜做** 3 条、**宜食** 2 条、**忌** 3 条，每条都附一句简短理由。
- 新增 **养生穴位**：每个时辰推荐 1 个位于当令经络上的穴位，并注明简单的按揉方法，例如辰时足三里、子时风池。
- 面板底部新增免责提示：“以上为传统养生参考，不能替代医疗诊断”。

### 变更
- **十二时辰宜忌全部重写**：各时辰之间不再重复，内容都与当令经络的生理特点相关。
- 信息面板宽度从 288px 加宽到 340px，并重新调整了排版和字号层级。
- 版本号升级为 1.1.0（程序集、清单、LivelyInfo）。
- README 更新了面板截图、桌面预览和十二时辰对照表。

## [1.0.0] - 2026-10-20

First public release. 首个正式版。

### Added
- **Holographic 3D body.** A translucent human model (based on the MakeHuman CC0 mesh, re-posed with palms forward) with soft key light, rim light, jade-like translucency, near/far depth shading and smooth crease darkening, plus a ground light disc and contact shadow.
- **Twelve meridians on the body.** All 12 regular meridians are traced on the surface as raised, shaded 3D tubes in their own colours, bilateral and following their textbook routes.
  - The meridian ruling the current Beijing-time 时辰 is shown as a gold glowing tube with particles flowing in the correct direction, traveling pulses and pulsing acupoints.
  - Depth-aware x-ray: parts of a meridian behind the body stay visible at a reduced intensity (about 30%; the ruling meridian about 45%).
- **Particle star-ring dial**:
  - about 32,000 GPU particles forming a stardust band and interwoven energy strands;
  - 12 constellation-style 时辰 nodes with branch characters and time ranges;
  - a dense, fast golden stream on the active segment;
  - a fine 24-hour scale;
  - a comet time pointer with a long tail and HH:MM tag;
  - a cyan seconds spark.
- **Glassmorphism info panel**: Beijing time, date, ruling meridian, route (start/end acupoints, direction), period progress, five 宜 and five 忌 items per 时辰, and a colour legend.
- **Continuous 360° body rotation**, with speed options 90 s / 60 s / 30 s per turn or stopped facing front (`?spin=`, tray menu).
- **Windows desktop app** (`ZiwuMeridianClock.exe`, .NET 8 WinForms + WebView2, single self-contained file):
  - Live wallpaper behind the desktop icons using the WorkerW technique, including the Windows 11 24H2+ Progman/DefView layout.
  - A bottom-most click-through fallback window.
  - Automatic re-embedding after an Explorer restart, display or DPI change, or resume from sleep.
  - Rendering pauses while a fullscreen app or game is in front, when hidden, or on the lock screen; frame rate drops on battery or in battery saver.
  - Tray menu: 显示/隐藏, 开机自启 (HKCU Run), 画质 高/中/低, 旋转速度, all monitors, pause on fullscreen, display mode, 安全模式, open log folder, 退出.
  - Single-instance guard, a WebView2 runtime check with download link, logging to `%LOCALAPPDATA%\ZiwuMeridianClock\log.txt`, and settings in `settings.json`.
- **Responsive layout** for 1366×768 through 4K and high-DPI or ultrawide screens, respecting the taskbar work area. The left ~40% of the screen stays clear for desktop icons.
- **Quality levels**:
  - 高: 30 fps, full particles, MSAA ×4;
  - 中: 24 fps, 60% particles, FXAA;
  - 低: 15 fps, 35% particles, FXAA.
- **Offline**: all assets are local, including subsetted Noto CJK fonts and the vendored three.js.
- **Lively Wallpaper fallback**: `web/` folder with `LivelyInfo.json`.

### Fixed
- **Flickering black blotches on some GPUs.** The cause was z-fighting between the depth pre-pass and the hologram surface, made worse by MSAA. Fixes:
  - polygon offset on the depth pre-pass and an identical vertex transform in both passes;
  - tighter near/far planes;
  - guards against NaN/Inf in every shader (normalize/pow/clamp);
  - meridian polylines cleaned (duplicate points and reversal spikes removed) and tube geometry scanned for bad values;
  - a NaN/Inf scrub pass before bloom;
  - softened crease darkening;
  - MSAA only on 高.
- **Discordant flickering rim.** Removed the per-frame random sparkle, softened and lowered the fresnel rim, slowed the scanlines, made the point twinkle gentler, and reduced the wireframe.
- The ruling meridian is no longer hidden when the body faces away.
- The 子 time label no longer touches the head, the bottom "12" numeral no longer sits under the taskbar, and the active node is no longer drowned out by its glow.

### Debug
- `?safe=1` (or tray → 高级 → 安全模式) disables crease darkening, MSAA and bloom to help isolate GPU-specific problems.
- `?nancheck=1` reports NaN/Inf pixel counts of the scene render target.

[Unreleased]: https://github.com/jeson998/ziwu-meridian-clock/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/jeson998/ziwu-meridian-clock/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/jeson998/ziwu-meridian-clock/releases/tag/v1.0.0
