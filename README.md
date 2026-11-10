# Folder Word Printer

轻量 Windows 中文工具。递归扫描多个文件夹中的 .doc 和 .docx，勾选后调用 Windows 注册的 Word 打印操作。

1. 解压 Release 的 ZIP，双击 FolderWordPrinter.exe。
2. 添加或拖入多个文件夹，点击「扫描 Word」。
3. 勾选要打印的文件。默认演练只检查文件可读且非空，不能验证 Word 能否正常打开文件。
4. 在 Windows 设置中确认默认打印机。关闭演练模式，重新勾选文件，再点击「开始」并确认。

需要 Windows 10 / 11 x64、自带的 .NET Framework、已安装的 Microsoft Word 和可用的打印机。可以先在资源管理器中对 Word 文件试用「显示更多选项 → 打印」，确认当前关联应用支持这个操作。

程序会逐个发出打印请求，使用 Windows 默认打印机及其打印首选项。请求成功只表示 Windows 接收了该操作，实际队列、出纸和打印顺序由 Word 和打印机处理。若 Word 弹出受保护视图、密码或其他提示，请在 Word 中处理。

停止按钮影响后续文件。已发出的请求请在打印队列查看。日志位于 %LOCALAPPDATA%\FolderWordPrinter。

从仓库根目录运行 build.ps1 可编译并运行核心扫描测试。项目使用 MIT 许可。
