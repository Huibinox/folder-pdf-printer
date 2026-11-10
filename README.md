# Folder PDF Printer

Windows 单文件 Python 工具。添加多个目录后自动递归查找 PDF，可确认清单后批量发出打印请求。需要公司已批准的 Python（带 Tkinter）、Adobe Acrobat Reader DC 和可用的 Windows 默认打印机。

1. 下载 Release 里的 pdf_batch_print.py，在脚本所在目录运行 py pdf_batch_print.py。
2. 在资源管理器打开目标目录，按 Ctrl+L、Ctrl+C 复制路径，粘贴到工具里并点击「添加路径」。支持映射盘和 UNC 路径。浏览器链接需通过公司批准的同步或映射功能在资源管理器中打开。
3. 添加、移除目录或改变子目录选项时会自动扫描。相同路径只列一次。文件变化后可点「重新扫描」。
4. 列表默认全选，按 Ctrl 或 Shift 可调整选择。「检查选中文件」检查能否读取文件及识别 PDF 文件头。
5. 将 Adobe Acrobat Reader 设为 Windows 默认 PDF 应用，确认默认实体打印机，点击「打印选中文件」并确认。

打印使用 Windows 为 PDF 注册的 Adobe 打印操作。列表记录请求是否发出，实际进度在 Windows 打印队列查看。停止仅影响后续请求。扫描不会发起打印，也不会持续监控目录。
