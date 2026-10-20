# 贡献指南 / Contributing

感谢你对 **子午流注·经络全息钟** 的关注！欢迎提交 Issue 和 Pull Request。

## 开发环境
- **网页部分**（`web/`）：用任意静态服务器预览，例如在 `web/` 下执行 `python -m http.server 8765`，然后打开：
  - `http://127.0.0.1:8765/index.html?t=10:24:36&preview=1`
  - `t=HH:MM:SS` 固定时间，`preview=1` 显示模拟桌面，`rot=角度` 固定转角，`spin=秒/圈`，`q=high|mid|low`，`safe=1`。
  - 不能直接用 `file://` 打开（ES Module 会被浏览器拦截）。
- **Windows 程序**（`src/ZiwuMeridianClock`）：需要 .NET SDK 8。
  - 在 Windows 上运行 `.\scripts\build.ps1`，或在 Linux/macOS 上运行 `scripts/build.sh` 交叉编译。
  - 调试 WebView2：设置环境变量 `ZIWU_DEVTOOLS=1` 后启动程序，即可使用开发者工具。

## 提交规范
- Commit message 使用 [Conventional Commits](https://www.conventionalcommits.org/)：`feat:`、`fix:`、`docs:`、`refactor:`、`chore:` 等。
- 用户可见的改动请同时更新 `CHANGELOG.md` 的 `[Unreleased]` 小节。
- 视觉改动请在 PR 中附截图或录屏（建议至少包含正面、背面两个角度）。

## 经络与中医内容
- 修改经络路线、穴位或宜忌文字时，请在 PR 中注明参考资料（如《经络腧穴学》教材、WHO 标准针灸穴位定位）。
- 本项目只做文化与科普展示，请勿加入诊断或治疗类表述。

## 代码风格
- 遵循 `.editorconfig`：C# 用 4 空格缩进，JS/HTML 用 2 空格。
- `web/lib/` 是原样引入的 three.js，请勿修改；升级时整体替换，并更新 `THIRD_PARTY_NOTICES.md`。
- 原生调用（P/Invoke）必须做好防御：失败要记日志，不能导致崩溃。

## 行为准则
请友善、尊重他人。对于骚扰或歧视性言论，维护者会删除并封禁。

---

**English:** PRs and issues are welcome!
- Preview the page with any static server (see the parameters above).
- Build the app with `scripts/build.ps1` on Windows or `scripts/build.sh` for a cross-build.
- Use Conventional Commits, update `CHANGELOG.md`, and attach screenshots for visual changes.
- Cite sources for any change to TCM content.
