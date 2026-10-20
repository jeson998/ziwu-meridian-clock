# Third-Party Notices / 第三方组件声明

The project's own code is under the MIT License (see `LICENSE`). It bundles or depends on the third-party components below. All of them allow redistribution under the terms listed. The full license texts are in `licenses/`.

本项目自身代码采用 MIT 许可证。以下第三方组件按各自许可证随附或引用。

| 组件 Component | 用途 Usage | 来源 Source | 许可证 License |
|---|---|---|---|
| **three.js r160** (`web/lib/three.module.js` and the addons under `web/lib/jsm/…`: EffectComposer, RenderPass, ShaderPass, MaskPass, Pass, UnrealBloomPass, OutputPass, CopyShader, LuminosityHighPassShader, OutputShader, FXAAShader) | WebGL 渲染、后期 Bloom/FXAA | https://github.com/mrdoob/three.js | MIT — `licenses/three.js-LICENSE.txt` |
| **MakeHuman base mesh `hm08` + targets** (`asian-male-young`, `universal-male-young-maxmuscle-averageweight`). Derived mesh in `web/models/body.bin` / `body.json`; sources in `tools/makehuman/` | 人体模型（重新摆姿、三角化、导出） | MakeHuman Community — https://github.com/makehumancommunity/makehuman (`makehuman/data/3dobjs/base.obj`, `makehuman/data/targets/…`), http://www.makehumancommunity.org | **CC0 1.0** (public domain dedication; the file headers say “explicitly released as CC0 in September 2020”). Original copyright (C) 2014 Manuel Bastioni / MakeHuman team. Credit is given voluntarily. — `licenses/CC0-1.0.txt` |
| **Noto Sans CJK SC / Noto Serif CJK SC** (subset → `web/fonts/sans-cjk.woff2`, `serif-cjk.woff2`, `serif-cjk-bold.woff2`) | 中文正文 / 衬线标题 | https://github.com/notofonts/noto-cjk (Google & Adobe; same design as Source Han Sans/Serif) | SIL Open Font License 1.1 — `licenses/OFL-1.1.txt` |
| **Noto Sans** (variable, Latin subset → `web/fonts/notosans-var.woff2`) | 数字 / 拉丁字符 | https://github.com/notofonts/latin-greek-cyrillic | SIL Open Font License 1.1 — `licenses/OFL-1.1.txt` |
| **Microsoft.Web.WebView2 SDK 1.0.2903.40** (NuGet; compiled into the exe, including `WebView2Loader.dll`) | 承载网页 | https://www.nuget.org/packages/Microsoft.Web.WebView2 | Microsoft BSD-style license — `licenses/WebView2-SDK-LICENSE.txt` |
| **.NET 8 runtime / Windows Forms** (self-contained in the exe) | 运行时 | https://github.com/dotnet/runtime, https://github.com/dotnet/winforms | MIT |
| **Microsoft Edge WebView2 Runtime** (not bundled; installed on the user's system) | 浏览器内核 | https://developer.microsoft.com/microsoft-edge/webview2/ | Microsoft software license (system component) |

## Notes on the fonts / 字体说明
- The WOFF2 files are **subsets** of the original fonts, containing only the characters the page uses. Subsetting is a "Modified Version" under the OFL.
- Noto fonts declare no Reserved Font Name, so the subset files keep their original internal names.
- The CSS aliases (`Ziwu Sans`, `Ziwu Serif`, `Ziwu Thin`) are local `@font-face` names only.
- The fonts remain under the OFL 1.1, and they may not be sold on their own.

## Notes on meridian data / 经络数据说明
- The meridian routes, acupoint landmarks and the 宜/忌 suggestions (`web/meridians.js`, `web/models/meridians3d.json`, `web/index.html`) were written for this project from standard TCM textbook descriptions. They are covered by this project's MIT license.
- TCM content is for cultural and educational reference only and is **not medical advice**.
