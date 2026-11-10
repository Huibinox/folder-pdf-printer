# Folder PDF Printer

Windows 单文件 Python 工具，可扫描多个目录中的 PDF。需要已批准的 Python（带 Tkinter）和 Adobe Acrobat Reader DC。

1. 下载 Release 的 pdf_batch_print.py，在脚本目录运行 py pdf_batch_print.py。
2. 从资源管理器复制文件夹路径并添加，支持映射盘和 UNC 路径。添加后自动扫描，默认包括子目录。
3. 先手动启动 Acrobat Reader。选中几份文件，点击「在 Acrobat 打开」，可通过 Adobe 的按路径打开接口验证多文件交接。
4. 确认 Acrobat 使用 Windows 默认实体打印机，再选少量文件试用「打印选中文件」。

脚本直接调用 Adobe Reader 的文档接口。每次打印后在 Windows 队列中查找本用户的新任务，匹配文件名后才处理下一份。队列名称不同、权限不足或任务离开队列过快，都可能导致无法确认。此时脚本停止，需先检查队列和出纸结果再决定是否重试。

当前版本是试用版。本机验证了 Acrobat 接受两份打开请求、实际读取虚拟打印队列，以及模拟三份打印任务逐份确认。尚未验证公司环境中的实际出纸。
