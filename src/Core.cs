using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace FolderWordPrinter {
public static class Core {
    public static void RequestPrint(string file) {
        var info = new ProcessStartInfo(Path.GetFullPath(file)) {
            UseShellExecute = true,
            Verb = "print"
        };
        using (Process.Start(info)) { }
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
            foreach (var f in Directory.GetFiles(dir)) {
                string ext = Path.GetExtension(f);
                if (!Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal) &&
                    (ext.Equals(".doc", StringComparison.OrdinalIgnoreCase) || ext.Equals(".docx", StringComparison.OrdinalIgnoreCase)))
                    files.Add(Path.GetFullPath(f));
            }
            if (recursive) foreach (var sub in Directory.GetDirectories(dir)) {
                try { if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) Walk(sub, true, files, visited, warning); }
                catch (Exception ex) { warning(sub + ": " + ex.Message); }
            }
        } catch (UnauthorizedAccessException ex) { warning(dir + ": " + ex.Message); }
          catch (IOException ex) { warning(dir + ": " + ex.Message); }
    }
}
public class NaturalComparer : IComparer<string> {
    public int Compare(string a, string b) { return StrCmpLogicalW(a, b); }
    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int StrCmpLogicalW(string a, string b);
}
}
