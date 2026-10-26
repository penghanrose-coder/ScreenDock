using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ScreenDock {
 public static class Native {
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct Device {
   public int cb; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string name;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string description;
   public int flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string id;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string key;
  }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct Mode {
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string device;
   public ushort spec; public ushort driver;
   public ushort size; public ushort extra;
   public uint fields; public int x; public int y;
   public uint orientation; public uint fixedOutput;
   public short color; public short duplex;
   public short yResolution; public short tt;
   public short collate;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string form;
   public ushort logPixels; public uint bits;
   public uint width; public uint height;
   public uint flags; public uint frequency;
   public uint icmMethod; public uint icmIntent;
   public uint media; public uint dither;
   public uint reserved1; public uint reserved2;
   public uint panWidth; public uint panHeight;
  }
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Info {
   public int size; public RectangleNative monitor,work; public uint flags;
   [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string device;
  }
  [StructLayout(LayoutKind.Sequential)] public struct RectangleNative { public int left,top,right,bottom; }
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Physical {
   public IntPtr handle; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string description;
  }
  public delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,IntPtr rect,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool EnumDisplayDevices(string device,uint index,ref Device result,uint flags);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool EnumDisplaySettings(string device,int number,ref Mode mode);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int ChangeDisplaySettingsEx(string device,ref Mode mode,IntPtr window,uint flags,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="ChangeDisplaySettingsExW")] public static extern int Apply(string device,IntPtr mode,IntPtr window,uint flags,IntPtr data);
  [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr monitor,ref Info info);
  [DllImport("dxva2.dll",SetLastError=true)] public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor,out uint count);
  [DllImport("dxva2.dll",SetLastError=true)] public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor,uint count,[Out] Physical[] physical);
  [DllImport("dxva2.dll")] public static extern bool DestroyPhysicalMonitors(uint count,Physical[] physical);
  [DllImport("dxva2.dll",SetLastError=true)] public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr monitor,byte code,out uint type,out uint current,out uint maximum);
  [DllImport("dxva2.dll",SetLastError=true)] public static extern bool SetVCPFeature(IntPtr monitor,byte code,uint value);
  [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool ReleaseCapture();
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
  public static Mode NewMode() { return new Mode { size=220,device="",form="" }; }
  public static Device NewDevice() { return new Device { cb=Marshal.SizeOf(typeof(Device)) }; }
  public static bool HasMonitor(string device,string identity){for(uint i=0;;i++){var child=NewDevice();if(!EnumDisplayDevices(device,i,ref child,0))return false;if(child.id==identity&&(child.flags&1)!=0)return true;}}
 }
 public class Saved {
  public string Device,Identity; public Native.Mode Mode;
 }
 public class Display {
  public string Device,Identity,Name; public bool Active,Primary; public Native.Mode Mode; public int Number;
 }
 public static class Backend {
  public static string DataPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenDock","layout.xml");
  public static List<Saved> Saved=new List<Saved>();
  public static string LoadWarning;
  public static void Load() {
   if(!File.Exists(DataPath)) return;
   try { using(var stream=File.OpenRead(DataPath)) Saved=(List<Saved>)new XmlSerializer(typeof(List<Saved>)).Deserialize(stream); }
   catch(Exception ex) { LoadWarning="布局记录未能读取："+ex.Message; }
  }
  public static void Remember(IEnumerable<Display> displays) {
   foreach(var d in displays.Where(d=>d.Active)) {
    Saved.RemoveAll(s=>s.Device==d.Device); Saved.Add(new Saved { Device=d.Device,Identity=d.Identity,Mode=d.Mode });
   }
   Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
   string temp=DataPath+".tmp";
   using(var stream=File.Create(temp)) new XmlSerializer(typeof(List<Saved>)).Serialize(stream,Saved);
   if(File.Exists(DataPath)) File.Replace(temp,DataPath,null); else File.Move(temp,DataPath);
  }
  public static List<Display> Enumerate() {
   var connected=DisplayInventory.Read();var candidates=new List<Tuple<Display,string>>();
   for(uint i=0;;i++){
    var dev=Native.NewDevice();if(!Native.EnumDisplayDevices(null,i,ref dev,0))break;if((dev.flags&8)!=0)continue;bool adapterActive=(dev.flags&1)!=0;
    for(uint j=0;;j++){
     var child=Native.NewDevice();if(!Native.EnumDisplayDevices(dev.name,j,ref child,0))break;var iface=Native.NewDevice();Native.EnumDisplayDevices(dev.name,j,ref iface,1);ConnectedTarget target=null;
     if(connected!=null){if(!connected.TryGetValue(iface.id??"",out target))continue;if(adapterActive){if(!target.Active||!String.Equals(target.Source,dev.name,StringComparison.OrdinalIgnoreCase)||(child.flags&1)==0)continue;}else if(target.Active)continue;}
     else if(!adapterActive||(child.flags&1)==0)continue;
     var mode=Native.NewMode();if(!Native.EnumDisplaySettings(dev.name,adapterActive?-1:-2,ref mode))mode=Native.NewMode();
     var display=new Display{Device=dev.name,Identity=child.id,Name=target!=null&&!String.IsNullOrWhiteSpace(target.Name)?target.Name:child.description,Active=adapterActive,Primary=adapterActive&&(dev.flags&4)!=0,Mode=mode};
     string key=!String.IsNullOrWhiteSpace(iface.id)?iface.id:child.id;candidates.Add(Tuple.Create(display,key));
    }
   }
   var result=new List<Display>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var item in candidates.OrderByDescending(x=>x.Item1.Active).ThenByDescending(x=>Saved.Any(s=>s.Device==x.Item1.Device&&s.Identity==x.Item1.Identity)).ThenByDescending(x=>x.Item1.Mode.width>0)){
    if(!seen.Add(item.Item2))continue;item.Item1.Number=result.Count+1;result.Add(item.Item1);
   }
   return result;
  }
  public static Native.Mode Disabled(Native.Mode mode) { mode.width=mode.height=0;mode.fields=0x180020;mode.extra=0;return mode; }
  public static Native.Mode RestoreMode(Display d) {
   var saved=Saved.Find(s=>s.Device==d.Device && s.Identity==d.Identity);
   var mode=saved==null?d.Mode:saved.Mode;
   if(mode.width==0 || mode.height==0) throw new Exception("没有可用的屏幕模式，请先在 Windows 显示设置中启用此屏幕。");
   mode.extra=0; return mode;
  }
  public static void Apply(Dictionary<string,Native.Mode> modes,string primaryDevice=null) {
   foreach(var pair in modes) {
    var mode=pair.Value; mode.extra=0;
    int result=Native.ChangeDisplaySettingsEx(pair.Key,ref mode,IntPtr.Zero,0x10000001u|(pair.Key==primaryDevice?0x10u:0u),IntPtr.Zero);
    if(result!=0) throw new Exception("驱动拒绝修改 "+pair.Key+"（返回码 "+result+"）。");
   }
   int applied=Native.Apply(null,IntPtr.Zero,IntPtr.Zero,0,IntPtr.Zero);
   if(applied!=0) throw new Exception("显示布局应用失败（返回码 "+applied+"）。");
  }
 }
 public class Confirm : Form {
  int remaining=15; System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(); Label text=new Label();
  string question;
  public Confirm(string prompt="屏幕布局已更改，还能正常看到界面吗？") {
   question=prompt;
   Text="确认屏幕布局";ClientSize=new Size(420,170);StartPosition=FormStartPosition.CenterScreen;BackColor=Art.Background;ForeColor=Art.Ink;Font=Art.Font(13);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;TopMost=true;
   text.SetBounds(24,22,375,60);Controls.Add(text);
   var keep=new SoftButton {Text="保留更改",DialogResult=DialogResult.OK};keep.SetBounds(222,105,170,40);Controls.Add(keep);
   var undo=new SoftButton {Text="恢复原布局",DialogResult=DialogResult.Cancel};undo.SetBounds(24,105,170,40);Controls.Add(undo);
   foreach(Button b in new[]{keep,undo}){b.BackColor=Art.Panel;b.ForeColor=Color.White;b.FlatStyle=FlatStyle.Flat;}
   CancelButton=undo;timer.Interval=1000;timer.Tick+=delegate{remaining--;UpdateText();if(remaining<=0){DialogResult=DialogResult.Cancel;Close();}};UpdateText();UiZoom.Attach(this,false,false);timer.Start();
  }
  void UpdateText(){text.Text=question+"\n"+remaining+" 秒内未确认，将自动恢复。";}
  protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
 }
 public partial class MainForm : Form {
  List<Display> displays; ListBox list=new ListBox(); Map map=new Map(); Label detail=new Label(),status=new Label();
  Button toggle,modes; NotifyIcon tray; bool quitting,busy; bool demo;
  Label screenName=new Label(),screenState=new Label();
  BrightnessSlider brightnessSlider=new BrightnessSlider();Label brightnessValue=new Label(),brightnessHint=new Label();Label brightnessSupport=new Label();
  Dictionary<string,int> brightnessValues=new Dictionary<string,int>();Dictionary<string,string> brightnessErrors=new Dictionary<string,string>();
  ToolTip supportTips=new ToolTip();
  void SetSupportLabel(Label label,Display d,bool supported,bool failed){label.Text=d==null?"—":!d.Active?"未启用":supported?"已支持":failed?"不支持":"检测中…";label.ForeColor=supported?Color.FromArgb(35,145,99):failed?Color.FromArgb(200,55,55):Art.Muted;}
  HashSet<string> brightnessPending=new HashSet<string>();
  bool BrightnessPending(Display d){return d!=null&&brightnessPending.Contains(BrightnessKey(d));}
  bool detecting,refreshPending,fullRefreshPending;
  System.Windows.Forms.Timer topologyTimer=new System.Windows.Forms.Timer{Interval=700};
  HashSet<string> pendingModes=new HashSet<string>();
  bool ModesPending(Display d){return d!=null&&pendingModes.Contains(BrightnessKey(d));}
  async Task DetectOne(Display d,SemaphoreSlim slots){
   await slots.WaitAsync();string key=BrightnessKey(d);
   try{
    try{int value=await Task.Run(()=>Brightness.Access(d.Device,d.Identity,null).Percent);if(IsDisposed)return;if(!BrightnessPending(d)&&(!busy||!brightnessValues.ContainsKey(key))){brightnessValues[key]=value;brightnessErrors.Remove(key);}}
    catch(Exception ex){if(IsDisposed)return;brightnessErrors[key]=ex.Message;}
    if(IsDisposed)return;SelectDisplay();
    try{var info=await Task.Run(()=>MonitorModes.Detect(d));if(IsDisposed)return;modeInfos[key]=info;modeErrors.Remove(key);d.Name=info.Model;list.Invalidate();}
    catch(Exception ex){if(IsDisposed)return;modeErrors[key]=ex.Message;}
   }finally{pendingModes.Remove(key);slots.Release();if(!IsDisposed)SelectDisplay();}
  }
  async Task DetectSupport(){
   if(demo||busy||detecting||IsDisposed)return;detecting=true;
   var targets=displays.Where(x=>x.Active).OrderByDescending(x=>x==Current).ToList();
   foreach(var d in targets)pendingModes.Add(BrightnessKey(d));SelectDisplay();
   try{using(var slots=new SemaphoreSlim(2)){await Task.WhenAll(targets.Select(d=>DetectOne(d,slots)).ToArray());}}
   finally{detecting=false;if(!IsDisposed){SelectDisplay();if(refreshPending&&!busy)RefreshDisplays(fullRefreshPending);}}
  }
  string BrightnessKey(Display d){return d.Device+"|"+d.Identity;}
  ModeCombo modeChoice=new ModeCombo();Label modeHint=new Label();Dictionary<string,ModeInfo> modeInfos=new Dictionary<string,ModeInfo>();Dictionary<string,string> modeErrors=new Dictionary<string,string>();
  public MainForm(bool preview,bool startInTray=false) {
   demo=preview;Text="ScreenDock";ClientSize=new Size(1160,780);FormBorderStyle=FormBorderStyle.Sizable;MinimumSize=new Size(760,570);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;ForeColor=Art.Ink;Font=Art.Font(14);Icon=Art.Icon();AutoScaleMode=AutoScaleMode.None;
   var menubar=CreateMainMenu();Controls.Add(menubar);MainMenuStrip=menubar;
   list.SetBounds(0,39,270,700);list.BackColor=Color.FromArgb(250,251,253);list.ForeColor=Art.Ink;list.BorderStyle=BorderStyle.None;list.ItemHeight=100;list.DrawMode=DrawMode.OwnerDrawFixed;list.IntegralHeight=false;list.DrawItem+=DrawDevice;list.SelectedIndexChanged+=delegate{SelectDisplay();};Controls.Add(list);
   Rule(272,32,1,710);LabelAt(this,"屏幕布局",300,46,240,33,19,true,Art.Ink);
   var refresh=Button("刷新设备",delegate{RefreshDisplays(true);});refresh.SetBounds(849,47,132,34);Controls.Add(refresh);
   var identify=Button("识别屏幕",delegate{Identify();});identify.SetBounds(995,47,139,34);Controls.Add(identify);
   map.SetBounds(300,94,834,190);Controls.Add(map);Rule(300,302,834,1);
   screenName.SetBounds(300,318,790,38);screenName.Font=Art.Font(23,true);Controls.Add(screenName);
   detail.SetBounds(300,358,790,30);detail.Font=Art.Font(15);detail.ForeColor=Art.Muted;Controls.Add(detail);Rule(300,400,834,1);
   LabelAt(this,"屏幕亮度",306,417,140,30,17,true,Art.Ink);brightnessSupport.SetBounds(447,417,100,30);brightnessSupport.Font=Art.Font(14);Controls.Add(brightnessSupport);
   brightnessSlider.SetBounds(650,416,410,34);Controls.Add(brightnessSlider);brightnessValue.SetBounds(1070,417,64,30);brightnessValue.TextAlign=ContentAlignment.MiddleRight;brightnessValue.Font=Art.Font(17);Controls.Add(brightnessValue);
   brightnessHint.SetBounds(306,453,820,20);brightnessHint.ForeColor=Art.Muted;brightnessHint.Font=Art.Font(11);Controls.Add(brightnessHint);Rule(300,482,834,1);
   LabelAt(this,"桌面连接",306,499,180,30,17,true,Art.Ink);LabelAt(this,"停用后，Windows 会重新安排此屏幕上的窗口。",306,532,590,24,13,false,Art.Muted);
   screenState.SetBounds(802,503,164,30);screenState.Font=Art.Font(12);Controls.Add(screenState);toggle=Button("停用此屏幕",delegate{Toggle();});toggle.SetBounds(980,508,154,40);Controls.Add(toggle);Rule(300,571,834,1);
   LabelAt(this,"内置模式",306,587,180,30,17,true,Art.Ink);modeHint.SetBounds(306,620,460,24);modeHint.Font=Art.Font(12);modeHint.ForeColor=Art.Muted;Controls.Add(modeHint);
   modeChoice.SetBounds(780,597,202,34);modeChoice.DropDownStyle=ComboBoxStyle.DropDownList;modeChoice.Font=Art.Font(14);modeChoice.DrawMode=DrawMode.OwnerDrawFixed;modeChoice.ItemHeight=27;modeChoice.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();if(e.Index>=0)TextRenderer.DrawText(e.Graphics,modeChoice.Items[e.Index].ToString(),modeChoice.Font,e.Bounds,Art.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};Controls.Add(modeChoice);
   modes=Button("应用",async delegate{await ApplyInlineMode();});modes.SetBounds(998,594,136,39);Controls.Add(modes);
   brightnessSlider.ValueChanged+=delegate{brightnessValue.Text=brightnessSlider.Value+"%";};brightnessSlider.Committed+=async delegate{await AdjustBrightness(brightnessSlider.Value);};
   AddEyeControls();Rule(0,756,1160,1);status.SetBounds(17,758,1126,22);status.Font=Art.Font(12);status.ForeColor=Art.Muted;Controls.Add(status);
   foreach(Control c in Controls){if(c.Top>=302)c.Top+=66;else if(c==list||(c.Width==1&&c.Height>600))c.Height+=66;}ClientSize=new Size(1160,846);
   // Keep the selected monitor heading above its resolution and refresh controls.
   screenName.Top-=66;detail.Top-=66;foreach(Control c in Controls)if(c is Panel&&c.Top==368&&c.Height==1)c.Top=298;
   foreach(Control c in Controls)if(c.Top>=466)c.Top+=44;AddInlineLayout();
   // Screen selection across the top, with all settings in the full-width area below.
   foreach(Control c in Controls){
    if(c==list||c is MenuStrip)continue;
    if(c.Width==1&&c.Height>600){c.Visible=false;continue;}
    if(c.Left>=300&&c.Left<600)c.Left-=276;
    if(c.Width>=600&&c!=status&&c.Left>0)c.Width+=276;
    if(c.Top>=46)c.Top+=110;
   }
   list.MultiColumn=true;list.ColumnWidth=360;list.SetBounds(24,42,1110,100);ClientSize=new Size(1160,1000);
   map.Pick=delegate(Display d){list.SelectedIndex=displays.FindIndex(x=>x.Device==d.Device);};
   trayMenu=CreateTrayMenu();
   tray=new NotifyIcon{Icon=Icon,Text="ScreenDock · 屏幕管理",Visible=!demo};tray.MouseUp+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Right)ShowTrayMenu(Cursor.Position);};tray.DoubleClick+=delegate{ShowWindow();};tray.BalloonTipClicked+=delegate{ShowWindow();};
   Resize+=delegate{if(WindowState==FormWindowState.Minimized){Hide();if(!demo)tray.ShowBalloonTip(4000,"ScreenDock","已最小化到系统托盘，双击托盘图标可重新打开。",ToolTipIcon.Info);}};
   if(startInTray&&!demo){Opacity=0;ShowInTaskbar=false;}
   Shown+=async delegate{if(startInTray&&!demo){Hide();Opacity=1;ShowInTaskbar=true;}await DetectSupport();};FormClosing+=delegate(object s,FormClosingEventArgs e){if(!quitting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
   topologyTimer.Tick+=delegate{if(busy||detecting||brightnessPending.Count>0)return;topologyTimer.Stop();try{RefreshDisplays(true);}catch(Exception ex){status.Text="刷新显示器失败："+ex.Message;}};
   UiZoom.Attach(this,false,false,true);RefreshDisplays();if(Backend.LoadWarning!=null)status.Text=Backend.LoadWarning;
  }
  void Rule(int x,int y,int w,int h){Controls.Add(new Panel{Bounds=new Rectangle(x,y,w,h),BackColor=Color.FromArgb(222,225,230)});}
  static void LabelAt(Control parent,string text,int x,int y,int w,int h,float size,bool bold,Color color){parent.Controls.Add(new Label{Text=text,Bounds=new Rectangle(x,y,w,h),Font=Art.Font(size,bold),ForeColor=color,BackColor=parent is Card?Color.White:parent.BackColor,TextAlign=ContentAlignment.MiddleLeft});}
  void DrawDevice(object sender,DrawItemEventArgs e){
   if(e.Index<0||displays==null||e.Index>=displays.Count)return;float z=UiZoom.Factor(list);Func<int,int> s=x=>(int)Math.Round(x*z);var d=displays[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;var g=e.Graphics;var r=e.Bounds;
   using(var b=new SolidBrush(selected?Color.FromArgb(227,239,255):list.BackColor))g.FillRectangle(b,r);
   if(selected)using(var b=new SolidBrush(Art.Accent))g.FillRectangle(b,r.X,r.Y+s(8),s(4),r.Height-s(16));
   var screen=new Rectangle(r.X+s(17),r.Y+s(28),s(48),s(34));using(var b=new SolidBrush(Color.FromArgb(73,80,88)))g.FillRectangle(b,screen);using(var pen=new Pen(Color.FromArgb(40,45,50)))g.DrawRectangle(pen,screen);
   using(var f=Art.Font(17*z))Art.Text(g,d.Number.ToString(),f,Color.White,screen,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
   using(var f=Art.Font(13*z,true))Art.Text(g,d.Name,f,Art.Ink,new Rectangle(r.X+s(78),r.Y+s(22),r.Width-s(86),s(26)));
   using(var f=Art.Font(12*z))Art.Text(g,d.Active?d.Mode.width+" × "+d.Mode.height+"  "+d.Mode.frequency+" Hz":"已停用",f,Art.Muted,new Rectangle(r.X+s(78),r.Y+s(52),r.Width-s(86),s(26)));
  }
  Button Button(string text,EventHandler action){var b=new SoftButton{Text=text};b.Click+=action;return b;}
  public void ShowWindow(){Show();if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Activate();}
  void Identify(){if(demo||busy)return;foreach(var screen in Screen.AllScreens){var d=displays.Find(x=>x.Device==screen.DeviceName);if(d==null)continue;var badge=new Form{FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(screen.Bounds.X+40,screen.Bounds.Y+40,160,110),BackColor=Art.Panel,TopMost=true,ShowInTaskbar=false};badge.Controls.Add(new Label{Dock=DockStyle.Fill,Text=d.Number.ToString(),TextAlign=ContentAlignment.MiddleCenter,ForeColor=Art.Ink,Font=new Font("Segoe UI",42,FontStyle.Bold)});var timer=new System.Windows.Forms.Timer{Interval=2000};timer.Tick+=delegate{timer.Stop();badge.Close();timer.Dispose();};badge.Show();timer.Start();}}
  void RefreshDisplays(bool full=false){
   if(busy){refreshPending=true;fullRefreshPending|=full;return;}if(detecting||brightnessPending.Count>0){refreshPending=true;fullRefreshPending|=full;return;}full|=fullRefreshPending;refreshPending=fullRefreshPending=false;if(full){MonitorModes.ClearCache();if(!layoutDirty)layoutModes.Clear();}string selected=Current==null?null:Current.Device;
   brightnessValues.Clear();brightnessErrors.Clear();modeInfos.Clear();modeErrors.Clear();shownModeInfo=null;
   displays=demo?new List<Display>{new Display{Device="demo1",Name="Philips 27M2N6800ML",Active=true,Primary=true,Number=1,Mode=new Native.Mode{width=2560,height=1440,frequency=144}},new Display{Device="demo2",Name="Philips 275M1RZN",Active=true,Number=2,Mode=new Native.Mode{x=2560,y=180,width=1920,height=1080,frequency=60}}}:Backend.Enumerate();
   list.Items.Clear();foreach(var d in displays)list.Items.Add("屏幕 "+d.Number+"   "+(d.Primary?"主屏":d.Active?"已启用":"已停用"));
   ReloadInlineLayout();if(displays.Count>0)list.SelectedIndex=Math.Max(0,displays.FindIndex(d=>d.Device==selected));else SelectDisplay();
   status.Text=demo?"演示界面 · 示例设备数据":"检测到 "+displays.Count+" 个显示设备 · "+displays.Count(d=>d.Active)+" 个正在使用 · 关闭窗口会保留托盘图标";map.Invalidate();if(IsHandleCreated&&!demo)BeginInvoke(new Action(async delegate{await DetectSupport();}));
  }
  Display Current {get{return displays!=null && list.SelectedIndex>=0 && list.SelectedIndex<displays.Count?displays[list.SelectedIndex]:null;}}
  void SelectDisplay(){UpdateInlineLayout();var d=Current;toggle.Enabled=d!=null&&!busy&&!detecting&&brightnessPending.Count==0&&!d.Primary;
   UpdateInlineModes();
   UpdateBrightnessUi();UpdateEyeUi();
   if(d==null){screenName.Text="未连接显示器";screenState.Text="";detail.Text="未检测到可管理的显示器。";map.Selected=null;map.Invalidate();return;}
   screenName.Text=d.Name;screenState.Text=d.Primary?"●  使用中  ·  主屏受保护":d.Active?"●  使用中":"○  已停用";screenState.ForeColor=d.Active?Color.FromArgb(46,145,116):Art.Muted;detail.Text=(d.Active?d.Mode.width+" × "+d.Mode.height+"  ·  "+d.Mode.frequency+" Hz":"当前已停用")+(d.Primary?"  ·  主屏受保护":"");
   toggle.Text=d.Active?"停用此屏幕":"启用此屏幕";map.Selected=d.Device;map.Invalidate();}
  void Toggle(){
   var d=Current;if(d==null||busy||detecting||demo||BrightnessPending(d))return;
   if(d.Primary){status.Text="主屏受保护；请先在 Windows 显示设置中更换主显示器。";return;}
   if(brightnessPending.Count>0)return;var fresh=Backend.Enumerate();var target=fresh.Find(x=>x.Device==d.Device&&x.Identity==d.Identity);
   if(target==null||target.Primary){RefreshDisplays();status.Text="屏幕状态已变化，请重新选择。";return;}
   var before=fresh.ToDictionary(x=>x.Device,x=>x.Active?x.Mode:Backend.Disabled(x.Mode));
   busy=true;SelectDisplay();bool attempted=false;
   try {
    Backend.Remember(fresh);var mode=target.Active?Backend.Disabled(target.Mode):Backend.RestoreMode(target);
    attempted=true;Backend.Apply(new Dictionary<string,Native.Mode>{{target.Device,mode}});
    using(var confirm=new Confirm()){if(confirm.ShowDialog(this)!=DialogResult.OK){Backend.Apply(before);attempted=false;status.Text="已恢复原布局。";}else{attempted=false;status.Text="已保留新布局。";}}
   }catch(Exception ex){string error=ex.Message;if(attempted){try{Backend.Apply(before);}catch(Exception rollback){error+="\n自动恢复失败："+rollback.Message+"\n请使用 Windows 显示设置恢复。";}}MessageBox.Show(this,error,"未能完成屏幕切换",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   finally{busy=false;RefreshDisplays();}
  }
  void UpdateBrightnessUi(){
   var d=Current;int value=0;string key=d==null?"":BrightnessKey(d);bool known=d!=null&&brightnessValues.TryGetValue(key,out value);
   if(demo&&d!=null){known=true;value=d.Number==1?70:65;}
   brightnessSlider.Enabled=known&&d!=null&&d.Active&&!busy&&!BrightnessPending(d);
   brightnessSlider.Value=known?value:0;brightnessValue.Text=known?value+"%":"—";
   SetSupportLabel(brightnessSupport,d,known,brightnessErrors.ContainsKey(key));supportTips.SetToolTip(brightnessSupport,brightnessErrors.ContainsKey(key)?brightnessErrors[key]:"已读取亮度接口；实际调节效果取决于显示器。");brightnessHint.Text=BrightnessPending(d)?"正在应用此屏幕亮度，可切换到其他屏幕操作…":d==null?"请选择显示器":!d.Active?"请先启用屏幕。":brightnessErrors.ContainsKey(key)?"当前连接不支持调节；可刷新设备重试。":known?"松开滑块应用 · 支持方向键微调":"正在自动读取亮度…";
  }
  async Task AdjustBrightness(int? percent){
   var d=Current;if(d==null||!d.Active||busy||BrightnessPending(d))return;
   if(demo){if(percent.HasValue)brightnessValue.Text=percent+"%";return;}
   string key=BrightnessKey(d);if(percent.HasValue&&!brightnessValues.ContainsKey(key))return;
   brightnessPending.Add(key);SelectDisplay();status.Text="屏幕 "+d.Number+(percent.HasValue?"：正在应用亮度…":"：正在读取亮度…");
   try{
    var result=await Task.Run(()=>Brightness.Access(d.Device,d.Identity,percent));
    if(IsDisposed)return;brightnessValues[key]=result.Percent;brightnessErrors.Remove(key);
    status.Text="屏幕 "+d.Number+"：显示器返回亮度 "+result.Percent+"%"+(percent.HasValue&&Math.Abs(result.Percent-percent.Value)>2?"，与请求值不同；显示器可能限制调节或延迟生效。":"。");
   }catch(Exception ex){if(IsDisposed)return;brightnessValues.Remove(key);brightnessErrors[key]=ex.Message;status.Text="屏幕 "+d.Number+"："+ex.Message;}
   finally{brightnessPending.Remove(key);if(!IsDisposed){SelectDisplay();if(refreshPending&&!busy&&!detecting&&brightnessPending.Count==0)RefreshDisplays(fullRefreshPending);}}
  }
  ModeInfo shownModeInfo;string shownModeKey;
  void UpdateInlineModes(){var d=Current;string key=d==null?"":BrightnessKey(d);ModeInfo info=null;if(d!=null)modeInfos.TryGetValue(key,out info);
   if(demo&&d!=null&&info==null){info=new ModeInfo();info.Current[0x14]=5;foreach(uint v in new uint[]{1,4,5,6,8})info.Options.Add(new ModeOption{Code=0x14,Value=v,Label=MonitorModes.Label(0x14,v)});modeInfos[key]=info;}
   if(shownModeKey!=key||shownModeInfo!=info){modeChoice.Items.Clear();if(info!=null){foreach(var o in info.Options)modeChoice.Items.Add(o);if(info.Options.Count>0)modeChoice.SelectedIndex=Math.Max(0,info.Options.FindIndex(x=>info.Current.ContainsKey(x.Code)&&info.Current[x.Code]==x.Value));}shownModeInfo=info;shownModeKey=key;}
   bool available=d!=null&&d.Active&&info!=null&&info.Options.Count>0;modeChoice.Enabled=modes.Enabled=available&&!busy&&!BrightnessPending(d)&&!ModesPending(d)&&!EyeOn(d);
   modeHint.Text=d==null?"请选择显示器":!d.Active?"请先启用屏幕。":available?"应用显示器内置模式。":info!=null||modeErrors.ContainsKey(key)?"未检测到可用模式；可在屏幕菜单中查看诊断。":"正在自动检测内置模式…";
  }
  async Task ApplyInlineMode(){var d=Current;var option=modeChoice.SelectedItem as ModeOption;if(d==null||option==null||busy||demo||BrightnessPending(d))return;busy=true;SelectDisplay();ModeSnapshot backup=null;bool attempted=false;string failure=null;
   try{status.Text="正在备份显示器设置…";backup=await Task.Run(()=>MonitorModes.Capture(d,option));MonitorModes.SaveRecovery(d,backup);attempted=true;await Task.Run(()=>MonitorModes.WithMonitor(d,h=>{MonitorModes.SetChecked(h,option.Code,option.Value);return true;}));using(var confirm=new Confirm("显示器模式已切换，是否保留？")){if(confirm.ShowDialog(this)!=DialogResult.OK){await Task.Run(()=>MonitorModes.Restore(d,backup));status.Text="已恢复切换前设置。";}else status.Text="已保留模式。可在屏幕菜单中恢复上次设置。";attempted=false;}}
   catch(Exception ex){failure=ex.Message;}
   if(failure!=null){if(attempted&&backup!=null){try{await Task.Run(()=>MonitorModes.Restore(d,backup));failure+="\n已恢复切换前设置。";}catch(Exception restore){failure+="\n恢复失败："+restore.Message;}}MessageBox.Show(this,failure,"模式切换",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   busy=false;RefreshDisplays();
  }
  void OpenModes(){var d=Current;if(d==null||busy||detecting||demo||BrightnessPending(d))return;if(EyeOn(d)){status.Text="请先关闭护眼模式并恢复设置，再调整内置模式。";return;}busy=true;SelectDisplay();try{using(var dialog=new ModeDialog(d))dialog.ShowDialog(this);}finally{busy=false;RefreshDisplays();}}
  public void Screenshot(string file){Show();if(demo&&list.Items.Count>1)list.SelectedIndex=1;Application.DoEvents();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(file);}quitting=true;Close();}
  protected override void WndProc(ref Message m){base.WndProc(ref m);if(!demo&&topologyTimer!=null&&!IsDisposed&&!Disposing&&(m.Msg==0x007E||(m.Msg==0x0219&&m.WParam.ToInt64()==0x0007))){topologyTimer.Stop();topologyTimer.Start();}}
  protected override void Dispose(bool disposing){if(disposing&&topologyTimer!=null)topologyTimer.Dispose();if(disposing&&tray!=null){tray.Visible=false;if(trayMenu!=null)trayMenu.Dispose();tray.Dispose();}base.Dispose(disposing);}
 }
 public static class Program {
  [STAThread] public static void Main(string[] args) {
   Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   if(args.Length>0 && args[0]=="--make-icon"){File.WriteAllBytes(args[1],Art.IconData());return;}
   if(args.Length>0 && args[0]=="--preview-modes"){using(var form=new ModeDialog(new Display(),true))form.Screenshot(args[1]);return;}
   if(args.Length>0 && args[0]=="--brightness-check"){
    if(Brightness.RawValue(20,220,0)!=20||Brightness.RawValue(20,220,50)!=120||Brightness.RawValue(20,220,100)!=220)throw new Exception("Brightness range conversion");
    var lines=new List<string>{"PASS: brightness percentage mapping with nonzero minimum.","Read-only brightness probe (no settings changed):"};
    foreach(var d in Backend.Enumerate().Where(x=>x.Active)){try{var b=Brightness.Access(d.Device,d.Identity,null);lines.Add(d.Device+": "+b.Percent+"% (raw "+b.Current+", min "+b.Minimum+", max "+b.Maximum+")");}catch(Exception ex){lines.Add(d.Device+": unavailable - "+ex.Message);}}
    File.WriteAllLines(args[1],lines);return;
   }
   if(args.Length>0 && args[0]=="--self-test"){
    if(Marshal.SizeOf(typeof(Native.Mode))!=220)throw new Exception("DEVMODE size");
    if(Marshal.OffsetOf(typeof(Native.Mode),"x").ToInt32()!=76||Marshal.OffsetOf(typeof(Native.Mode),"width").ToInt32()!=172||Marshal.OffsetOf(typeof(Native.Mode),"frequency").ToInt32()!=184)throw new Exception("DEVMODE offsets");
    var data=new List<Saved>{new Saved{Device="test",Identity="id",Mode=new Native.Mode{device="test",form="",size=220,x=-1920,width=1920,height=1080}}};
    var serializer=new XmlSerializer(typeof(List<Saved>));using(var stream=new MemoryStream()){serializer.Serialize(stream,data);stream.Position=0;var result=(List<Saved>)serializer.Deserialize(stream);if(result[0].Mode.x!=-1920||result[0].Mode.width!=1920)throw new Exception("Serialization");}
    var disabled=Backend.Disabled(data[0].Mode);if(disabled.width!=0||disabled.height!=0||disabled.fields!=0x180020)throw new Exception("Detach mode");
    var devices=Backend.Enumerate();File.WriteAllText(args[1],"PASS: native layout (220), saved layout round-trip, detach mode.\r\nRead-only enumeration: "+devices.Count+" display device(s).\r\n"+String.Join("\r\n",devices.Select(d=>d.Device+" | "+d.Name+" | active="+d.Active+" | "+d.Mode.width+"x"+d.Mode.height)));return;
   }
   if(args.Length>0 && args[0]=="--preview"){using(var f=new MainForm(true))f.Screenshot(args[1]);return;}
   bool created;using(var mutex=new Mutex(true,"Local\\ScreenDock.Desktop",out created)){if(!created){if(args.Contains("--tray"))return;MessageBox.Show("ScreenDock 已在运行，请双击系统托盘图标打开。","ScreenDock");return;}Backend.Load();Application.Run(new MainForm(false,args.Contains("--tray")));}
  }
 }
}











