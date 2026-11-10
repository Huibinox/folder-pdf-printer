# SPDX-License-Identifier: MIT
"""Scan PDFs and invoke the Acrobat Reader Windows Print action."""
import ctypes
from ctypes import wintypes
import getpass
import ntpath
import os
import queue
import re
import threading
import time
import tkinter as tk
from tkinter import filedialog, messagebox, ttk


def normalize_folder(value):
    value = value.strip().strip('"')
    if not value or "://" in value or not ntpath.isabs(value):
        raise ValueError("请输入资源管理器中的完整目录路径，例如 Z:\\部门 或 \\\\服务器\\共享。")
    drive, _ = ntpath.splitdrive(value)
    if not drive:
        raise ValueError("请输入包含盘符或服务器共享名的完整路径。")
    return ntpath.normpath(value)


def scan_folders(folders, recursive):
    files = {}
    errors = []
    for folder in folders:
        for root, dirs, names in os.walk(folder, onerror=errors.append):
            if not recursive:
                dirs.clear()
            else:
                dirs[:] = [
                    d for d in dirs
                    if not getattr(os.path, "isjunction", os.path.islink)(os.path.join(root, d))
                ]
            for name in names:
                if ntpath.splitext(name)[1].lower() == ".pdf":
                    path = os.path.join(root, name)
                    files.setdefault(ntpath.normcase(path), path)
    def sort_key(path):
        return [int(part) if part.isascii() and part.isdigit() else part.casefold()
                for part in re.split(r"([0-9]+)", path)]
    return sorted(files.values(), key=sort_key), errors


def require_acrobat_print_handler():
    # Query the executable Windows registered for the PDF print verb.
    query = ctypes.WinDLL("shlwapi.dll").AssocQueryStringW
    query.argtypes = (
        wintypes.DWORD, wintypes.DWORD, wintypes.LPCWSTR,
        wintypes.LPCWSTR, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD),
    )
    query.restype = ctypes.c_long
    size = wintypes.DWORD(32768)
    path = ctypes.create_unicode_buffer(size.value)
    result = query(0, 2, ".pdf", "print", path, ctypes.byref(size))
    if result != 0 or ntpath.basename(path.value).casefold() not in {"acrord32.exe", "acrobat.exe"}:
        raise OSError("Windows 当前没有将 PDF 的打印操作交给 Adobe Acrobat Reader。请将 Acrobat Reader 设为默认 PDF 应用后重试。")


def default_printer():
    get_name = ctypes.WinDLL("winspool.drv", use_last_error=True).GetDefaultPrinterW
    get_name.argtypes = (wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD))
    get_name.restype = wintypes.BOOL
    length = wintypes.DWORD()
    get_name(None, ctypes.byref(length))
    if not length.value:
        raise OSError("请先在 Windows 设置中指定默认打印机。")
    name = ctypes.create_unicode_buffer(length.value)
    if not get_name(name, ctypes.byref(length)):
        raise ctypes.WinError(ctypes.get_last_error())
    if any(label in name.value.casefold() for label in ("print to pdf", "adobe pdf", "xps", "onenote", "fax")):
        raise OSError(f"当前默认打印机为 {name.value}，请先选择实际打印机。")
    return name.value


class PrintUnconfirmed(Exception):
    """The request was made, but its queue entry could not be verified."""


class JobInfo(ctypes.Structure):
    _fields_ = (
        [("id", wintypes.DWORD)]
        + [(field, wintypes.LPWSTR) for field in
           ("printer", "machine", "user", "document", "datatype", "status_text")]
        + [(field, wintypes.DWORD) for field in
           ("status", "priority", "position", "total_pages", "pages_printed")]
        + [("submitted", wintypes.WORD * 8)]
    )


def queue_jobs(printer):
    spool = ctypes.WinDLL("winspool.drv", use_last_error=True)
    open_printer = spool.OpenPrinterW
    open_printer.argtypes = (wintypes.LPWSTR, ctypes.POINTER(wintypes.HANDLE), ctypes.c_void_p)
    open_printer.restype = wintypes.BOOL
    close_printer = spool.ClosePrinter
    close_printer.argtypes = (wintypes.HANDLE,)
    close_printer.restype = wintypes.BOOL
    enum_jobs = spool.EnumJobsW
    enum_jobs.argtypes = (
        wintypes.HANDLE, wintypes.DWORD, wintypes.DWORD, wintypes.DWORD,
        ctypes.c_void_p, wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD), ctypes.POINTER(wintypes.DWORD),
    )
    enum_jobs.restype = wintypes.BOOL
    handle = wintypes.HANDLE()
    if not open_printer(printer, ctypes.byref(handle), None):
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        needed = wintypes.DWORD()
        count = wintypes.DWORD()
        size = 8192
        for _ in range(4):
            buffer = ctypes.create_string_buffer(size)
            if enum_jobs(handle, 0, 0xFFFFFFFF, 1, buffer, size, ctypes.byref(needed), ctypes.byref(count)):
                jobs = (JobInfo * count.value).from_buffer(buffer)
                return {job.id: (job.user or "", job.document or "", job.status) for job in jobs}
            error = ctypes.get_last_error()
            if error != 122:
                raise ctypes.WinError(error)
            size = max(size * 2, needed.value)
        raise OSError("打印队列在读取期间发生变化，请稍后重试。")
    finally:
        close_printer(handle)





def submit_and_confirm(path, printer, stop, on_sent, timeout=120):
    baseline = queue_jobs(printer)
    if stop.is_set():
        return None
    if default_printer() != printer:
        raise OSError("默认打印机发生变化，请重新确认。")
    os.startfile(path, "print")
    on_sent()
    deadline = time.monotonic() + timeout
    filename = ntpath.basename(path).casefold()
    titles = {filename, ntpath.splitext(filename)[0]}
    current_user = getpass.getuser().casefold()
    confirmed = None
    while not stop.is_set() and time.monotonic() < deadline:
        try:
            jobs = queue_jobs(printer)
        except OSError as exc:
            raise PrintUnconfirmed(f"无法读取打印队列。{exc}") from exc
        if confirmed is None:
            for job_id, (user, document, _) in jobs.items():
                owner = user.replace("/", "\\").rsplit("\\", 1)[-1].split("@", 1)[0].casefold()
                title = ntpath.basename(document.strip().strip('"')).casefold()
                if job_id not in baseline and owner == current_user and title in titles:
                    confirmed = job_id
                    break
        if confirmed is not None:
            # JOB_STATUS_SPOOLING; a fast job can also leave the queue after being observed.
            if confirmed not in jobs or not jobs[confirmed][2] & 0x0008:
                return confirmed
        stop.wait(0.2)
    reason = "已停止等待" if stop.is_set() else "等待入队或缓冲超时"
    raise PrintUnconfirmed(f"{reason}。请检查打印队列和出纸结果后，再决定是否重试。")


class App:
    def __init__(self, window):
        self.window = window
        self.events = queue.Queue()
        self.stop_requested = threading.Event()
        self.paths = {}
        self.busy = False
        self.controls = []
        window.title("PDF 批量打印 · v4.0.3 逐份确认")
        window.geometry("880x580")
        window.minsize(640, 440)
        outer = ttk.Frame(window, padding=14)
        outer.pack(fill="both", expand=True)
        ttk.Label(outer, text="文件夹路径（支持映射盘和 UNC 共享路径）").pack(anchor="w")
        row = ttk.Frame(outer)
        row.pack(fill="x", pady=5)
        self.entry = ttk.Entry(row)
        self.entry.pack(side="left", fill="x", expand=True)
        self.entry.bind("<Return>", lambda _: self.add_folder())
        self.button(row, "添加路径", self.add_folder)
        self.button(row, "浏览", self.browse)
        self.button(row, "移除", self.remove_folder)
        self.folders = tk.Listbox(outer, height=4)
        self.folders.pack(fill="x")
        row = ttk.Frame(outer)
        row.pack(fill="x", pady=6)
        self.recursive = tk.BooleanVar(value=True)
        toggle = ttk.Checkbutton(row, text="包含子文件夹", variable=self.recursive, command=self.refresh)
        toggle.pack(side="left")
        self.controls.extend([self.entry, toggle])
        self.button(row, "重新扫描", self.scan)
        self.button(row, "全选", lambda: self.files.selection_set(self.files.get_children()))
        self.button(row, "清除选中", lambda: self.files.selection_remove(self.files.selection()))
        body = ttk.Frame(outer)
        body.pack(fill="both", expand=True)
        self.files = ttk.Treeview(body, columns=("file", "folder", "status"), show="headings")
        for column, title, width in (("file", "PDF 文件", 210), ("folder", "所在目录", 400), ("status", "状态", 155)):
            self.files.heading(column, text=title)
            self.files.column(column, width=width, minwidth=80)
        self.files.pack(side="left", fill="both", expand=True)
        scroll = ttk.Scrollbar(body, orient="vertical", command=self.files.yview)
        scroll.pack(side="right", fill="y")
        self.files.configure(yscrollcommand=scroll.set)
        row = ttk.Frame(outer)
        row.pack(fill="x", pady=6)
        self.button(row, "检查选中文件", lambda: self.run_batch(dry=True))
        self.button(row, "打印选中文件", lambda: self.run_batch(dry=False))
        self.stop_button = ttk.Button(row, text="停止后续任务", command=self.stop_requested.set, state="disabled")
        self.stop_button.pack(side="left", padx=4)
        self.pause_after_print = tk.BooleanVar(value=False)
        pause_toggle = ttk.Checkbutton(row, text="每份关掉 Acrobat 文档后继续", variable=self.pause_after_print)
        pause_toggle.pack(side="left", padx=4)
        self.controls.append(pause_toggle)
        self.status = tk.StringVar(value="粘贴网盘目录的完整路径，添加后自动扫描。")
        ttk.Label(outer, textvariable=self.status, wraplength=610).pack(anchor="w")
        window.protocol("WM_DELETE_WINDOW", self.close)
        window.after(60, self.process_events)

    def button(self, parent, title, action):
        button = ttk.Button(parent, text=title, command=action)
        button.pack(side="left", padx=4)
        self.controls.append(button)

    def add_folder(self, value=None):
        if self.busy:
            return
        try:
            path = normalize_folder(self.entry.get() if value is None else value)
        except ValueError as exc:
            messagebox.showwarning("路径无效", str(exc), parent=self.window)
            return
        if ntpath.normcase(path) not in {ntpath.normcase(p) for p in self.folders.get(0, "end")}:
            self.folders.insert("end", path)
            self.refresh()
        self.entry.delete(0, "end")

    def browse(self):
        path = filedialog.askdirectory(parent=self.window)
        if path:
            self.add_folder(path)

    def remove_folder(self):
        for index in reversed(self.folders.curselection()):
            self.folders.delete(index)
        self.refresh()

    def refresh(self):
        if self.folders.size():
            self.scan()
        else:
            self.clear_files()

    def clear_files(self):
        self.files.delete(*self.files.get_children())
        self.paths.clear()
        self.status.set("添加一个文件夹开始扫描。")

    def work(self, action, cancellable=False):
        self.busy = True
        self.stop_requested.clear()
        for control in self.controls:
            control.configure(state="disabled")
        if cancellable:
            self.stop_button.configure(state="normal")

        def worker():
            try:
                action()
            except Exception as exc:
                self.events.put(("error", str(exc)))
            finally:
                self.events.put(("done", None))

        threading.Thread(target=worker, daemon=True).start()

    def scan(self):
        folders = self.folders.get(0, "end")
        if not folders:
            messagebox.showinfo("扫描", "请先添加文件夹路径。", parent=self.window)
            return
        self.clear_files()
        self.status.set("正在读取目录…")
        recursive = self.recursive.get()
        self.work(lambda: self.events.put(("scanned", scan_folders(folders, recursive))))

    def run_batch(self, dry):
        selected = set(self.files.selection())
        batch = [(item, path) for item, path in self.paths.items() if item in selected]
        if not batch:
            messagebox.showinfo("选择文件", "请在列表中选中要处理的文档。", parent=self.window)
            return
        printer = None
        if not dry:
            try:
                require_acrobat_print_handler()
                printer = default_printer()
            except OSError as exc:
                messagebox.showerror("打印设置", str(exc), parent=self.window)
                return
            if not messagebox.askokcancel("确认打印", f"向 {printer} 发出 {len(batch)} 个打印请求？", parent=self.window):
                return

        pause_between = not dry and self.pause_after_print.get()

        def process():
            ok = failed = uncertain = 0
            for index, (item, path) in enumerate(batch):
                if self.stop_requested.is_set():
                    break
                try:
                    with open(path, "rb") as document:
                        if b"%PDF-" not in document.read(1024):
                            raise OSError("未找到 PDF 文件头")
                    if not dry:

                        def waiting():
                            self.events.put(("row", (item, "已请求，等待入队和缓冲")))

                        job = submit_and_confirm(path, printer, self.stop_requested, waiting)
                        if job is None:
                            break
                    ok += 1
                    status = "可读取" if dry else f"已入队 #{job}"
                    self.events.put(("row", (item, status)))
                    if pause_between and index + 1 < len(batch):
                        resume = threading.Event()
                        self.events.put(("pause_for_close", (ntpath.basename(path), resume)))
                        while not self.stop_requested.is_set() and not resume.wait(0.1):
                            pass
                except PrintUnconfirmed as exc:
                    uncertain += 1
                    self.events.put(("row", (item, "未确认，已暂停")))
                    self.events.put(("error", f"{path}\n\n{exc}"))
                    break

                except OSError as exc:
                    failed += 1
                    self.events.put(("row", (item, f"失败 · {exc}")))
                    if not dry:
                        self.events.put(("error", f"{path}\n\n{exc}"))
                        break
            outcome = "检查通过" if dry else "确认入队"
            remaining = len(batch) - ok - failed - uncertain
            self.events.put(("summary", f"{outcome} {ok}，失败 {failed}，未确认 {uncertain}，未处理 {remaining}。"))

        self.status.set("正在检查…" if dry else "正在发出打印请求…")
        self.work(process, cancellable=True)

    def process_events(self):
        while True:
            try:
                event, data = self.events.get_nowait()
            except queue.Empty:
                break
            if event == "scanned":
                paths, errors = data
                for path in paths:
                    item = self.files.insert("", "end", values=(ntpath.basename(path), ntpath.dirname(path), "待处理"))
                    self.paths[item] = path
                self.files.selection_set(self.files.get_children())
                self.status.set(f"找到 {len(paths)} 份 PDF 文件。目录错误 {len(errors)} 处。")
                if errors:
                    details = "\n".join(str(error) for error in errors[:5])
                    messagebox.showwarning("部分目录无法读取", details, parent=self.window)
            elif event == "row":
                item, status = data
                self.files.set(item, "status", status)
                self.status.set(status)
            elif event == "pause_for_close":
                filename, resume = data
                try:
                    if not self.stop_requested.is_set():
                        self.status.set(f"{filename} 已入队，等待关闭 Acrobat 标签页。")
                        proceed = messagebox.askokcancel(
                            "关闭当前 PDF 后继续",
                            f"{filename} 已入队。\n\n请切换到 Acrobat，关闭这份 PDF 的标签页。"
                            "回来点「确定」再打印下一份。点「取消」结束本批后续任务。",
                            parent=self.window,
                        )
                        if not proceed:
                            self.stop_requested.set()
                finally:
                    resume.set()
            elif event == "summary":
                self.status.set(data)
            elif event == "error":
                messagebox.showerror("处理失败", data, parent=self.window)
            elif event == "done":
                self.busy = False
                for control in self.controls:
                    control.configure(state="normal")
                self.stop_button.configure(state="disabled")
        self.window.after(60, self.process_events)

    def close(self):
        if not self.busy:
            self.window.destroy()
        else:
            self.stop_requested.set()
            self.status.set("请等当前操作结束后关闭。")


if __name__ == "__main__":
    window = tk.Tk()
    app = App(window)
    window.mainloop()
