using System;
using System.IO;
using System.Collections.Generic;
using FolderPdfPrinter;
class Tests {
    static void Assert(bool v,string message) { if(!v) throw new Exception(message); }
    static void Main(string[] args) {
        if(args.Length>0 && args[0]=="--fake-engine") {
            if(args[1]=="sleep") { System.Threading.Thread.Sleep(10000);return; }
            if(args[1]=="fail") { Environment.ExitCode=7;return; }
            File.WriteAllLines(args[1],new[]{args[2],args[3]});return;
        }
        string root=Path.Combine(Path.GetTempPath(),"FolderPdfPrinter-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try {
            var child=Directory.CreateDirectory(Path.Combine(root,"中文 子目录")).FullName;
            File.WriteAllText(Path.Combine(root,"2.PDF"),"%PDF-1.4");File.WriteAllText(Path.Combine(root,"10.pdf"),"%PDF-1.4");
            File.WriteAllText(Path.Combine(root,"ignore.txt"),"");File.WriteAllText(Path.Combine(child,"a.pdf"),"%PDF-1.4");
            var warnings=new List<string>();
            var flat=Core.Scan(new[]{root,root},false,warnings.Add);Assert(flat.Count==2,"flat scan + duplicate root");Assert(Path.GetFileName(flat[0])=="2.PDF","natural order");
            var nested=Core.Scan(new[]{root,child},true,warnings.Add);Assert(nested.Count==3,"overlapping roots deduplicated");
            Core.Scan(new[]{Path.Combine(root,"missing")},true,warnings.Add);Assert(warnings.Count==1,"missing directory reported");
            var printArgs=Core.Arguments(Path.Combine(child,"a.pdf"),"Office Printer 中文",2,"duplexlong","monochrome","A4","fit");
            Assert(printArgs.Contains("2x,collate,fit,duplexlong,monochrome,paper=A4"),"settings");Assert(printArgs.Contains("\"Office Printer 中文\""),"printer quoting");Assert(printArgs.EndsWith("a.pdf\""),"file quoting");
            Assert(Core.Quote("a\\")=="\"a\\\\\"","trailing slash");Assert(Core.Quote("a\"b")=="\"a\\\"b\"","embedded quote");
            bool invalid=false;try { Core.Arguments("a.pdf","P",0,"","","","fit"); } catch(ArgumentOutOfRangeException) { invalid=true; }Assert(invalid,"invalid copies rejected");
            string self=System.Reflection.Assembly.GetExecutingAssembly().Location;
            string captured=Path.Combine(root,"args.txt");
            Core.RunEngine(self,"--fake-engine "+Core.Quote(captured)+" "+Core.Quote("中文 printer\"name")+" "+Core.Quote("C:\\folder with space\\"),5000);
            var roundtrip=File.ReadAllLines(captured);Assert(roundtrip[0]=="中文 printer\"name" && roundtrip[1]=="C:\\folder with space\\","actual process argument roundtrip");
            bool nonzero=false;try { Core.RunEngine(self,"--fake-engine fail",5000); } catch(IOException) { nonzero=true; }Assert(nonzero,"nonzero engine error");
            bool timeout=false;try { Core.RunEngine(self,"--fake-engine sleep",100); } catch(TimeoutException) { timeout=true; }Assert(timeout,"engine timeout handled");
            Console.WriteLine("PASS: scan, recursion, deduplication, natural order, Chinese paths, warnings, settings, quoting, validation, actual process argument roundtrip, engine failure and timeout.");
        } finally { Directory.Delete(root,true); }
    }
}

