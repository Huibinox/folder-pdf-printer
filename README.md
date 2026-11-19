# Folder PDF Printer

A single-file Python tool for Windows that scans multiple folders for PDFs and sends selected files to Adobe Acrobat Reader DC for batch printing. Requires an approved Python installation with Tkinter and Adobe Acrobat Reader DC.

## Usage

1. Download `pdf_batch_print.py` from [Releases](https://github.com/Huibinox/folder-pdf-printer/releases). In its folder, run `py pdf_batch_print.py`.
2. Copy folder paths from File Explorer and add them. Mapped drives and UNC paths are supported. Scanning starts automatically after adding a folder and includes subfolders by default.
3. Start Acrobat Reader manually. Select a few files and click Open in Acrobat (`在 Acrobat 打开`) to check that Adobe's path-based interface accepts multiple files. If Reader cannot be connected, select one file and click Copy PDF Path (`复制 PDF 路径`), then press Ctrl+O in Reader and paste the full path into the filename field.
4. Confirm that Acrobat uses the Windows default physical printer. Try Print Selected Files (`打印选中文件`) with a small selection first.

The software interface remains in Chinese; the labels above identify its existing buttons.

## Print confirmation

The script calls Adobe Reader's document interface directly. After each print request, it looks for a new job belonging to the current user in the Windows print queue and matches the filename before continuing. Different queue names, insufficient permissions, or jobs leaving the queue too quickly can prevent confirmation. In that case, the script stops. Check the queue and physical output before deciding whether to retry.

## Verification limits

This is a trial version. Local checks confirmed that Acrobat accepted two open requests, that a virtual print queue could be read, and that three simulated print jobs were confirmed sequentially. Physical printing in the company environment has not been verified.
