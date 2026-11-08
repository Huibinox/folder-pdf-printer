# Folder PDF Printer

Windows 中文 PDF 批量打印工具。添加多个文件夹，检查文件清单，然后依次提交打印。

## 使用

1. 从 Releases 下载 Windows x64 ZIP，解压整个文件夹。不要直接在压缩包内运行。
2. 双击 `FolderPdfPrinter.exe`。同目录保留 `SumatraPDF.exe`。
3. 添加文件夹（也可拖入文件夹），设置是否包含子文件夹，点击「扫描 PDF」。
4. 勾选需要的文件，选择打印机、份数、单双面、颜色、纸张和缩放。
5. 默认开启「演练模式」，只检查文件可读性及 PDF 文件头，**不提交打印，也不验证完整 PDF 内容或驱动能力**。
6. 演练通过后重新勾选文件，取消演练模式，点击开始并确认。

文件按完整路径自然排序（2 在 10 前），重叠文件夹内同一路径只列一次。不同路径的同名文件会保留；不会按内容去重。子目录中的链接 / junction 不会递归跟随。

成功处理的文件会自动取消勾选，避免同一批次误重复提交；重新扫描会重建清单并重新勾选全部文件。不会跨会话自动跳过历史文件。

## 打印与停止

- 「已提交」表示打印引擎正常退出，不代表实际出纸。请先用少量文件确认设备行为。
- 单双面、颜色及纸张设置取决于打印机驱动支持，默认跟随打印机。
- 「停止后续任务」等待当前文件结束；已提交任务需在 Windows 打印队列取消。
- 每份 PDF 独立提交，不保证与其他程序并发提交的作业隔离。
- 实际打印遇到错误即停止后续文件；每个文件最长等待 180 秒，超时后状态不确定，可能已经部分提交。检查打印队列后再决定是否重试。
- 含 PDF / XPS / OneNote 的打印机名称会被拦截，避免弹出另存为窗口。其他自定义虚拟打印机也可能弹窗，不支持无人值守使用。
- 不支持密码输入；加密、损坏或禁止打印的 PDF 可能失败。
- 日志保存在 `%LOCALAPPDATA%\FolderPdfPrinter`，包含本机文件路径；程序不上传 PDF。

## 系统与构建

Windows 10 / 11 x64，.NET Framework 4.x（Windows 通常自带），无需 Python。运行 `powershell -ExecutionPolicy Bypass -File build.ps1` 编译并运行核心测试。运行目录需提供官方 SumatraPDF 3.6.1 x64 portable，重命名为 `SumatraPDF.exe`。

打印接口：[SumatraPDF 官方命令行文档](https://www.sumatrapdfreader.org/docs/Command-line-arguments)。

应用源代码使用 MIT 许可。便携包中的独立 SumatraPDF 使用 GPLv3；其完整对应源代码及许可在同一 Release 的 `SumatraPDF-3.6.1-source.zip`、`SumatraPDF-COPYING`，详见 `THIRD_PARTY.md`。
