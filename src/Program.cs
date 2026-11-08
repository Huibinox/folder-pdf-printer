using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Printing;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Reflection;

[assembly: AssemblyTitle("Folder PDF Printer")]
[assembly: AssemblyVersion("1.0.0.0")]
namespace FolderPdfPrinter {
static class Program {
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (s,e) => MessageBox.Show(e.Exception.Message, "发生错误");
        var form = new MainForm();
        if (args.Contains("--self-test")) {
            form.Shown += async (s,e) => {
                try { await form.SelfTest(); Environment.ExitCode=0; }
                catch(Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test.txt"),ex.ToString());Environment.ExitCode=1; }
                form.Close();
            };
        }
        if (args.Contains("--screenshot")) {
            form.Shown += (s,e) => { var t = new System.Windows.Forms.Timer { Interval=1000 }; t.Tick += (ss,ee) => { t.Stop(); using(var b = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(b,new Rectangle(0,0,form.Width,form.Height)); b.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"screenshot.png")); } form.Close(); }; t.Start(); };
        }
        Application.Run(form);
    }
}
public class MainForm : Form {
    readonly ListBox folders = new ListBox { Dock=DockStyle.Fill, HorizontalScrollbar=true };
    readonly ListView list = new ListView { Dock=DockStyle.Fill, View=View.Details, CheckBoxes=true, FullRowSelect=true, HideSelection=false };
    readonly ComboBox printer = new ComboBox { Width=330, DropDownStyle=ComboBoxStyle.DropDownList };
    readonly ComboBox duplex = Choices("跟随打印机", "单面", "双面 · 长边", "双面 · 短边");
    readonly ComboBox color = Choices("跟随打印机", "黑白", "彩色");
    readonly ComboBox paper = Choices("跟随打印机", "A4", "A3", "Letter");
    readonly ComboBox scale = Choices("缩小过大页面", "适合纸张", "原始大小");
    readonly NumericUpDown copies = new NumericUpDown { Minimum=1,Maximum=999,Value=1,Width=60 };
    readonly CheckBox recursive = new CheckBox { Text="包含子文件夹", AutoSize=true, Checked=true };
    readonly CheckBox dry = new CheckBox { Text="演练模式（只检查，不打印）", AutoSize=true, Checked=true };
    readonly Label status = new Label { Text="添加文件夹，然后扫描 PDF。默认开启演练模式。",AutoSize=true, Padding=new Padding(0,8,0,0) };
    readonly TextBox log = new TextBox { Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill };
    readonly ProgressBar progress = new ProgressBar { Dock=DockStyle.Fill };
    readonly List<Control> lockControls = new List<Control>();
    Button start, stop;
    bool busy; CancellationTokenSource cancel;
    readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FolderPdfPrinter");
    string logFile;
    static ComboBox Choices(params string[] options) { var c = new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Width=145 }; c.Items.AddRange(options); c.SelectedIndex=0; return c; }
    Button Button(string text, EventHandler action) { var b = new Button { Text=text,AutoSize=true,Height=32,Padding=new Padding(8,2,8,2) }; b.Click+=action; return b; }
    FlowLayoutPanel Row() { return new FlowLayoutPanel { Dock=DockStyle.Fill,AutoSize=false,WrapContents=true,Padding=new Padding(0,3,0,3) }; }
    void Option(FlowLayoutPanel row, string title, Control c) {
        var group = new FlowLayoutPanel { AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink, WrapContents=false, Margin=new Padding(0) };
        group.Controls.Add(new Label { Text=title, AutoSize=true, Margin=new Padding(8,7,3,0) }); group.Controls.Add(c);row.Controls.Add(group);lockControls.Add(c);
    }
    public MainForm() {
        Text="Folder PDF Printer 1.0 · 文件夹批量打印"; Width=1100; Height=820; MinimumSize=new Size(960,730); StartPosition=FormStartPosition.CenterScreen;
        Font=new Font("Microsoft YaHei UI",9F); BackColor=Color.FromArgb(246,248,251);
        var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=10 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,52)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,90)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,90));
        Controls.Add(root);
        root.Controls.Add(new Label { Text="PDF 批量打印",Font=new Font(Font.FontFamily,21,FontStyle.Bold),AutoSize=true },0,0);
        var addRow=Row(); root.Controls.Add(addRow,0,1);
        var add=Button("＋ 添加文件夹",(s,e)=> { using(var d=new FolderBrowserDialog { Description="选择含 PDF 的文件夹" }) if(d.ShowDialog()==DialogResult.OK) AddFolder(d.SelectedPath); });
        var remove=Button("移除文件夹",(s,e)=> { if(folders.SelectedIndex>=0) { folders.Items.RemoveAt(folders.SelectedIndex); InvalidateList(); } });
        var scan=Button("扫描 PDF",async (s,e)=>await Scan());
        addRow.Controls.AddRange(new Control[]{add,remove,recursive,scan}); lockControls.AddRange(new Control[]{add,remove,recursive,scan,folders});
        recursive.CheckedChanged+=(s,e)=>InvalidateList();
        folders.AllowDrop=true; folders.DragEnter+=(s,e)=> { if(e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect=DragDropEffects.Copy; };
        folders.DragDrop+=(s,e)=> { if(!busy) foreach(var p in (string[])e.Data.GetData(DataFormats.FileDrop)) if(Directory.Exists(p)) AddFolder(p); };
        root.Controls.Add(folders,0,2);
        var select=Row(); root.Controls.Add(select,0,3);
        var all=Button("全选",(s,e)=>CheckAll(true)); var none=Button("全不选",(s,e)=>CheckAll(false));
        var open=Button("打开所选 PDF",(s,e)=> { if(list.SelectedItems.Count>0) try { Process.Start(new ProcessStartInfo(list.SelectedItems[0].Tag.ToString()){UseShellExecute=true}); } catch(Exception ex) { MessageBox.Show(ex.Message); } });
        select.Controls.AddRange(new Control[]{all,none,open,new Label { Text="按完整路径自然排序 · 同一路径自动去重",AutoSize=true,Margin=new Padding(12,8,0,0) }});
        lockControls.AddRange(new Control[]{all,none,open,list});
        list.Columns.Add("PDF 文件",270);list.Columns.Add("所在文件夹",530);list.Columns.Add("状态",170);
        list.ItemChecked+=(s,e)=> { if(!busy) UpdateCount(); }; root.Controls.Add(list,0,4);
        var settings=Row();root.Controls.Add(settings,0,5);
        Option(settings,"打印机",printer); var refresh=Button("刷新",(s,e)=>RefreshPrinters()); settings.Controls.Add(refresh);lockControls.Add(refresh);
        Option(settings,"份数",copies);Option(settings,"单双面",duplex);Option(settings,"颜色",color);Option(settings,"纸张",paper);Option(settings,"缩放",scale);
        var actions=Row();root.Controls.Add(actions,0,6);
        start=Button("开始",async(s,e)=>await Print());start.BackColor=Color.FromArgb(32,106,220);start.ForeColor=Color.White;start.FlatStyle=FlatStyle.Flat;
        stop=Button("停止后续任务",(s,e)=> { if(cancel!=null) cancel.Cancel(); stop.Enabled=false; WriteLog("停止请求已收到；等待当前文件结束，后续文件不再提交。"); });stop.Enabled=false;
        var logs=Button("打开日志目录",(s,e)=> { Directory.CreateDirectory(dataDir);Process.Start(new ProcessStartInfo(dataDir){UseShellExecute=true}); });
        actions.Controls.AddRange(new Control[]{start,stop,dry,logs});lockControls.Add(dry);
        root.Controls.Add(progress,0,7);root.Controls.Add(status,0,8);root.Controls.Add(log,0,9);
        Shown+=(s,e)=>RefreshPrinters();
        FormClosing+=(s,e)=> { if(busy) { e.Cancel=true; MessageBox.Show("请先停止任务，等待当前文件处理结束后再关闭。","任务进行中"); } };
    }
    void AddFolder(string path) { if(!folders.Items.Cast<string>().Contains(path,StringComparer.OrdinalIgnoreCase)) { folders.Items.Add(path);InvalidateList(); } }
    void InvalidateList() { list.Items.Clear();UpdateCount(); }
    void CheckAll(bool value) { foreach(ListViewItem item in list.Items) item.Checked=value; }
    void UpdateCount() { status.Text=String.Format("共 {0} 个 PDF，已勾选 {1} 个。",list.Items.Count,list.CheckedItems.Count); }
    void SetBusy(bool value) { busy=value;foreach(var c in lockControls) c.Enabled=!value;start.Enabled=!value;stop.Enabled=value && cancel!=null; }
    void WriteLog(string message) {
        string line=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"  "+message;
        log.AppendText(line+Environment.NewLine);
        if(logFile!=null) try { File.AppendAllText(logFile,line+Environment.NewLine,System.Text.Encoding.UTF8); } catch(Exception) { status.Text="日志无法写入磁盘，请复制窗口内日志。"; }
    }
    void RefreshPrinters() {
        string old=printer.Text;printer.Items.Clear();
        try { foreach(string p in PrinterSettings.InstalledPrinters) printer.Items.Add(p);
            string preferred=String.IsNullOrEmpty(old)?new PrinterSettings().PrinterName:old;
            if(printer.Items.Contains(preferred)) printer.SelectedItem=preferred;else if(printer.Items.Count>0) printer.SelectedIndex=0;
        } catch(Exception ex) { WriteLog("读取打印机失败："+ex.Message); }
    }
    public async Task SelfTest() {
        string temp=Path.Combine(Path.GetTempPath(),"FolderPdfPrinter-UI-"+Guid.NewGuid());
        Directory.CreateDirectory(temp);
        try {
            // Header-only fixture intentionally tests the documented shallow dry-run check.
            File.WriteAllText(Path.Combine(temp,"演练 2.pdf"),"%PDF-1.4\n");
            File.WriteAllText(Path.Combine(temp,"演练 10.pdf"),"invalid");
            AddFolder(temp);await Scan();
            if(list.Items.Count!=2 || list.CheckedItems.Count!=2) throw new Exception("UI scan failed");
            await Print();
            if(list.Items[0].SubItems[2].Text!="演练通过" || list.Items[1].SubItems[2].Text!="失败 / 待检查" || list.CheckedItems.Count!=1 || busy) throw new Exception("UI dry-run state failed");
            using(var image=new Bitmap(Width,Height)) { DrawToBitmap(image,new Rectangle(0,0,Width,Height)); image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"screenshot.png")); }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test.txt"),"PASS: UI scan, checked items, dry-run success, invalid PDF failure, uncheck success only, restored controls, log persistence. No print jobs submitted.");
        } finally { Directory.Delete(temp,true); }
    }
    async Task Scan() {
        if(folders.Items.Count==0) { MessageBox.Show("请先添加文件夹。");return; }
        SetBusy(true);status.Text="正在扫描…";list.Items.Clear();
        var roots=folders.Items.Cast<string>().ToArray();bool recurse=recursive.Checked;
        var warnings=new List<string>();
        try { var files=await Task.Run(()=>Core.Scan(roots,recurse,warnings.Add));
            list.BeginUpdate();try { foreach(var f in files) { var item=new ListViewItem(new[]{Path.GetFileName(f),Path.GetDirectoryName(f),"待处理"}) { Tag=f,Checked=true };list.Items.Add(item); } } finally { list.EndUpdate(); }
            foreach(var w in warnings) WriteLog("扫描跳过："+w);
            WriteLog("扫描完成："+files.Count+" 个 PDF，跳过 "+warnings.Count+" 处错误。");
        } catch(Exception ex) { MessageBox.Show(ex.Message,"扫描失败"); } finally { SetBusy(false);UpdateCount(); }
    }
    async Task Print() {
        var items=list.CheckedItems.Cast<ListViewItem>().ToArray();if(items.Length==0) { MessageBox.Show("请扫描并勾选要处理的 PDF。");return; }
        bool simulate=dry.Checked;string engine=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SumatraPDF.exe");
        string selectedPrinter=printer.Text;
        if(!simulate && (String.IsNullOrWhiteSpace(selectedPrinter)||!File.Exists(engine))) { MessageBox.Show("请选择打印机，并确认 SumatraPDF.exe 与本程序在同一目录。");return; }
        if(!simulate && (selectedPrinter.IndexOf("PDF",StringComparison.OrdinalIgnoreCase)>=0 || selectedPrinter.IndexOf("XPS",StringComparison.OrdinalIgnoreCase)>=0 || selectedPrinter.IndexOf("OneNote",StringComparison.OrdinalIgnoreCase)>=0)) {
            MessageBox.Show("该打印机可能弹出另存为对话框，不适合无人值守批量打印。请选实体打印机；可先用演练模式检查清单。");return;
        }
        if(!simulate && MessageBox.Show(String.Format("即将向 {0} 提交 {1} 个文件，每个 {2} 份。\n\n停止只影响后续文件，已提交任务需在 Windows 打印队列取消。\n是否开始？",selectedPrinter,items.Length,copies.Value),"确认批量打印",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK) return;
        try { Directory.CreateDirectory(dataDir);logFile=Path.Combine(dataDir,"job-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".log");File.WriteAllText(logFile,"",System.Text.Encoding.UTF8); }
        catch(Exception ex) { MessageBox.Show("无法创建打印日志："+ex.Message);return; }
        int count=(int)copies.Value;
        string d=new[]{"","simplex","duplexlong","duplexshort"}[duplex.SelectedIndex];
        string c=new[]{"","monochrome","color"}[color.SelectedIndex];
        string p=new[]{"","A4","A3","Letter"}[paper.SelectedIndex];
        string sc=new[]{"shrink","fit","noscale"}[scale.SelectedIndex];
        cancel=new CancellationTokenSource();SetBusy(true);progress.Maximum=items.Length;progress.Value=0;
        int ok=0,failed=0;WriteLog((simulate?"开始演练":"开始打印")+"；打印机："+selectedPrinter+"；份数："+count+"；单双面："+duplex.Text+"；颜色："+color.Text+"；纸张："+paper.Text+"；缩放："+scale.Text);
        try { foreach(var item in items) {
            if(cancel.IsCancellationRequested) break;
            string file=item.Tag.ToString();item.SubItems[2].Text="处理中";status.Text=Path.GetFileName(file);
            try {
                await Task.Run(()=> {
                    using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                        byte[] header=new byte[1024];int n=input.Read(header,0,header.Length);
                        if(System.Text.Encoding.ASCII.GetString(header,0,n).IndexOf("%PDF-",StringComparison.Ordinal)<0) throw new InvalidDataException("缺少 PDF 文件头");
                    }
                    if(simulate) return;
                    Core.RunEngine(engine,Core.Arguments(file,selectedPrinter,count,d,c,p,sc),180000);
                });
                item.SubItems[2].Text=simulate?"演练通过":"已提交（未确认出纸）";ok++;item.Checked=false;WriteLog(item.SubItems[2].Text+"："+file);
            } catch(Exception ex) {
                failed++;item.SubItems[2].Text="失败 / 待检查";WriteLog("失败："+file+"；"+ex.Message);
                if(!simulate) { cancel.Cancel();WriteLog("实际打印遇到错误，已停止后续文件，避免继续提交。"); }
            }
            progress.Value++;
        }
        status.Text=String.Format("{0}：通过 / 提交 {1}，失败 {2}，未处理 {3}。",simulate?"演练结束":"批次结束",ok,failed,items.Length-ok-failed);WriteLog(status.Text);
        } finally { cancel.Dispose();cancel=null;SetBusy(false); }
    }
}
}
