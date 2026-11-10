using System;
using System.IO;
using System.Collections.Generic;
using FolderWordPrinter;
class Tests {
    static void Assert(bool v,string message) { if(!v) throw new Exception(message); }
    static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"FolderWordPrinter-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try {
            var child=Directory.CreateDirectory(Path.Combine(root,"中文 子目录")).FullName;
            File.WriteAllText(Path.Combine(root,"2.DOC"),"fixture");File.WriteAllText(Path.Combine(root,"10.docx"),"fixture");
            File.WriteAllText(Path.Combine(root,"ignore.pdf"),"");File.WriteAllText(Path.Combine(root,"~$draft.docx"),"");File.WriteAllText(Path.Combine(child,"a.docx"),"fixture");
            var warnings=new List<string>();
            var flat=Core.Scan(new[]{root,root},false,warnings.Add);Assert(flat.Count==2,"flat scan + duplicate root");Assert(Path.GetFileName(flat[0])=="2.DOC","natural order");
            var nested=Core.Scan(new[]{root,child},true,warnings.Add);Assert(nested.Count==3,"overlapping roots deduplicated");
            Core.Scan(new[]{Path.Combine(root,"missing")},true,warnings.Add);Assert(warnings.Count==1,"missing directory reported");
            Console.WriteLine("PASS: Word extensions, temporary file exclusion, recursion, deduplication, natural order, Chinese paths and warnings.");
        } finally { Directory.Delete(root,true); }
    }
}
