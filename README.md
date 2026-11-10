# Folder Word Printer

Windows 上的单文件 Python 工具，用于扫描多个目录中的 .doc 和 .docx，并调用 Windows 的 Word 打印操作。需要公司已批准的 Python（带 Tkinter）、Microsoft Word 和默认打印机。脚本仅使用 Python 标准库。

1. 下载 Release 里的 word_batch_print.py。
2. 在脚本所在目录运行 py word_batch_print.py。
3. 在资源管理器中打开网盘目标目录，按 Ctrl+L，再按 Ctrl+C。将复制的路径粘贴到脚本窗口，点「添加路径」。可添加多个目录，支持映射盘和 UNC 共享路径。
4. 扫描后默认选中全部结果。可在列表中用 Ctrl 或 Shift 调整选择，先点「检查选中文件」验证可读性，再打印。

可添加本机当前用户能访问的目录。浏览器中的网盘链接需要通过公司已批准的同步或映射功能，在资源管理器中以文件夹形式打开。扫描失败会显示系统错误。

正式打印沿用 Windows 默认打印机及其首选项。列表显示的是已发出的打印请求，队列与出纸状态请在 Windows 中查看。停止按钮只停止后续请求。
