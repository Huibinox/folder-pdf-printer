using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace FolderPdfPrinter {
public static class Core {
    public static void RunEngine(string engine, string arguments, int timeoutMs) {
        var info = new ProcessStartInfo(engine, arguments) { UseShellExecute=false, CreateNoWindow=true, WindowStyle=ProcessWindowStyle.Hidden };
        using (var process = Process.Start(info)) {
            if (!process.WaitForExit(timeoutMs)) {
                try { process.Kill(); process.WaitForExit(5000); } catch (Exception) { }
                throw new TimeoutException("超时，提交状态不确定；请检查打印队列，勿直接重试");
            }
            if (process.ExitCode != 0) throw new IOException("打印引擎退出码 " + process.ExitCode + "；请检查文件及打印机，可能部分提交");
        }
    }
    public static List<string> Scan(IEnumerable<string> roots, bool recursive, Action<string> warning) {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots) Walk(Path.GetFullPath(root), recursive, files, visited, warning);
        return files.OrderBy(x => x, new NaturalComparer()).ToList();
    }
    static void Walk(string dir, bool recursive, HashSet<string> files, HashSet<string> visited, Action<string> warning) {
        if (!visited.Add(dir)) return;
        try {
            foreach (var f in Directory.GetFiles(dir)) if (Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) files.Add(Path.GetFullPath(f));
            if (recursive) foreach (var sub in Directory.GetDirectories(dir)) {
                try { if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) Walk(sub, true, files, visited, warning); }
                catch (Exception ex) { warning(sub + ": " + ex.Message); }
            }
        } catch (UnauthorizedAccessException ex) { warning(dir + ": " + ex.Message); }
          catch (IOException ex) { warning(dir + ": " + ex.Message); }
    }
    public static string Quote(string s) {
        // Windows CommandLineToArgvW escaping; no command shell is involved.
        return "\"" + System.Text.RegularExpressions.Regex.Replace(s, "(\\\\*)\"", "$1$1\\\"").TrimEnd('\\') + new string('\\', s.Reverse().TakeWhile(c => c == '\\').Count() * 2) + "\"";
    }
    public static string Arguments(string file, string printer, int copies, string duplex, string color, string paper, string scale) {
        if (copies < 1 || copies > 999) throw new ArgumentOutOfRangeException("copies");
        if (String.IsNullOrWhiteSpace(printer)) throw new ArgumentException("请选择打印机");
        var settings = new List<string> { copies + "x", "collate", scale };
        if (duplex != "") settings.Add(duplex);
        if (color != "") settings.Add(color);
        if (paper != "") settings.Add("paper=" + paper);
        return "-print-to " + Quote(printer) + " -print-settings " + Quote(String.Join(",", settings)) + " -silent " + Quote(Path.GetFullPath(file));
    }
}
public class NaturalComparer : IComparer<string> {
    public int Compare(string a, string b) { return StrCmpLogicalW(a, b); }
    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int StrCmpLogicalW(string a, string b);
}
}
