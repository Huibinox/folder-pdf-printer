# Folder PDF Printer v4.0.3

单文件 Python 工具，适合从多个文件夹批量打印 PDF。基于 v4.0.1 的 Windows 打印关联，需要公司已批准的 Python（带 Tkinter）、默认 PDF 应用 Acrobat Reader DC，以及 Windows 默认实体打印机。

1. 下载 pdf_batch_print.py，在脚本目录运行 py pdf_batch_print.py。
2. 添加本机或网盘目录，工具会自动扫描子目录中的 PDF。可按 Ctrl 或 Shift 选择要打印的文件。
3. 点一次「打印选中文件」。当前文件在 Windows 队列出现且完成缓冲后，才发送下一份。

如果 Acrobat 留着上一份导致下一份打不开，勾选「每份关掉 Acrobat 文档后继续」。每份入队后，切到 Acrobat 关闭该份标签页，再回来点确定。

每份最多等待 120 秒。队列权限受限、驱动使用其他任务名、任务过快离开队列或网络延迟都可能导致无法确认。工具会停在该文件。先看队列和出纸情况，再决定补打哪些文件。「已入队」表示观察到打印任务，实际出纸由打印机完成。
