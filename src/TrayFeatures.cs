using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;using System.Reflection;using System.Runtime.InteropServices;

namespace ScreenDock {
 public static class TrayPosition {
  [StructLayout(LayoutKind.Sequential)]struct Identifier {public uint Size;public IntPtr Window;public uint Id;public Guid Guid;}
  [StructLayout(LayoutKind.Sequential)]struct Rect {public int Left,Top,Right,Bottom;}
  [DllImport("shell32.dll")]static extern int Shell_NotifyIconGetRect(ref Identifier id,out Rect rect);
  public static bool TryGet(NotifyIcon icon,out Rectangle bounds){bounds=Rectangle.Empty;if(icon==null||!icon.Visible)return false;var flags=BindingFlags.Instance|BindingFlags.NonPublic;var idField=typeof(NotifyIcon).GetField("id",flags);var windowField=typeof(NotifyIcon).GetField("window",flags);if(idField==null||windowField==null)return false;var window=windowField.GetValue(icon) as NativeWindow;if(window==null)return false;var id=new Identifier{Size=(uint)Marshal.SizeOf(typeof(Identifier)),Window=window.Handle,Id=Convert.ToUInt32(idField.GetValue(icon)),Guid=Guid.Empty};Rect rect;if(Shell_NotifyIconGetRect(ref id,out rect)!=0||rect.Right<=rect.Left||rect.Bottom<=rect.Top)return false;bounds=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);return true;}
 }
 public static class StartupRegistration {
  const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
  public static string Command(string path){return "\""+path+"\" --tray";}
  public static bool Enabled(){using(var key=Registry.CurrentUser.OpenSubKey(KeyPath))return key!=null&&String.Equals(key.GetValue("ScreenDock") as string,Command(Application.ExecutablePath),StringComparison.OrdinalIgnoreCase);}
  public static void Set(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(KeyPath)){if(enabled)key.SetValue("ScreenDock",Command(Application.ExecutablePath),RegistryValueKind.String);else key.DeleteValue("ScreenDock",false);}}
 }
 public partial class MainForm {
  MenuStrip CreateMainMenu(){
   var bar=new MenuStrip{Dock=DockStyle.None,Bounds=new Rectangle(0,0,1160,32),BackColor=Color.FromArgb(248,249,251),Font=Art.Font(14)};
   var file=new ToolStripMenuItem("文件(&F)");
   var exit=new ToolStripMenuItem("退出 ScreenDock",null,delegate{if(!busy&&brightnessPending.Count==0){quitting=true;Close();}});
   file.DropDownItems.Add(exit);file.DropDownOpening+=delegate{exit.Enabled=!busy&&brightnessPending.Count==0;};
   var screen=new ToolStripMenuItem("屏幕(&D)");
   screen.DropDownItems.Add(new ToolStripMenuItem("刷新设备",null,delegate{RefreshDisplays(true);}){ShortcutKeys=Keys.F5});
   screen.DropDownItems.Add(new ToolStripMenuItem("识别屏幕",null,delegate{Identify();}){ShortcutKeys=Keys.Control|Keys.I});
   screen.DropDownItems.Add(new ToolStripSeparator());

   var connection=new ToolStripMenuItem("停用此屏幕",null,delegate{Toggle();});screen.DropDownItems.Add(connection);
   var eye=new ToolStripMenuItem("开启护眼模式",null,async delegate{await ChangeEye(true);});screen.DropDownItems.Add(eye);
   var advanced=new ToolStripMenuItem("内置模式与诊断…",null,delegate{OpenModes();});screen.DropDownItems.Add(advanced);
   screen.DropDownOpening+=delegate{var d=Current;connection.Text=d!=null&&d.Primary?"主屏受保护":d!=null&&d.Active?"停用此屏幕":"启用此屏幕";connection.Enabled=toggle.Enabled;eye.Text=eyeToggle.Text=="开启"?"开启护眼模式":"关闭护眼并恢复";eye.Enabled=eyeToggle.Enabled;eye.Checked=EyeOn(d);advanced.Enabled=d!=null&&d.Active&&!busy&&!detecting&&!BrightnessPending(d)&&!EyeOn(d);};
   var settings=new ToolStripMenuItem("设置(&S)");var startup=new ToolStripMenuItem("开机启动（进入托盘）");settings.DropDownItems.Add(startup);
   settings.DropDownOpening+=delegate{try{startup.Checked=StartupRegistration.Enabled();startup.Enabled=true;}catch(Exception ex){startup.Enabled=false;startup.ToolTipText=ex.Message;}};
   startup.Click+=delegate{try{StartupRegistration.Set(!StartupRegistration.Enabled());startup.Checked=StartupRegistration.Enabled();status.Text=startup.Checked?"已开启开机启动，下次登录后进入托盘。":"已关闭开机启动。";}catch(Exception ex){MessageBox.Show(this,"无法修改开机启动："+ex.Message,"ScreenDock");}};
   settings.DropDownItems.Add(AutoUpdateItem());settings.DropDownItems.Add(new ToolStripSeparator());settings.DropDownItems.Add("Windows 显示设置…",null,delegate{System.Diagnostics.Process.Start("ms-settings:display");});
   var help=new ToolStripMenuItem("帮助(&H)");help.DropDownItems.Add(UpdateMenuItem());help.DropDownItems.Add(new ToolStripMenuItem("使用说明",null,delegate{System.Diagnostics.Process.Start(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"使用说明.md"));}){ShortcutKeys=Keys.F1});help.DropDownItems.Add("关于 ScreenDock",null,delegate{MessageBox.Show(this,"ScreenDock "+Updates.Current.ToString(3)+"\n多显示器管理工具 · MIT 开源许可\n最小化后可从系统托盘重新打开。","关于 ScreenDock");});
   bar.Items.AddRange(new ToolStripItem[]{file,screen,PresetMenu(),settings,help});return bar;
  }
  ContextMenuStrip trayMenu;
  ContextMenuStrip CreateTrayMenu(){var menu=new ContextMenuStrip{Font=SystemFonts.MenuFont};BuildTrayMenu(menu);return menu;}
  void ShowTrayMenu(Point anchor){
   if(trayMenu==null||trayMenu.IsDisposed)return;
   if(trayMenu.Visible)trayMenu.Close();
   trayMenu.SuspendLayout();try{BuildTrayMenu(trayMenu);}finally{trayMenu.ResumeLayout(true);}
   Rectangle iconBounds;if(TrayPosition.TryGet(tray,out iconBounds)){var iconArea=Screen.FromRectangle(iconBounds).WorkingArea;anchor=new Point(iconBounds.Left+iconBounds.Width/2,iconBounds.Top>=iconArea.Top+iconArea.Height/2?iconBounds.Top:iconBounds.Bottom);}
   var area=Screen.FromPoint(anchor).WorkingArea;
   // Build before Show: NotifyIcon's automatic placement uses the previous menu size.
   var direction=anchor.Y>=area.Top+area.Height/2?ToolStripDropDownDirection.AboveLeft:ToolStripDropDownDirection.BelowLeft;
   trayMenu.Show(anchor,direction);
  }
  bool PickTrayDisplay(Display target){if(displays==null)return false;int index=displays.FindIndex(x=>x.Device==target.Device&&x.Identity==target.Identity);if(index<0){tray.ShowBalloonTip(3000,"ScreenDock","显示设备已变化，请刷新设备。",ToolTipIcon.Info);return false;}list.SelectedIndex=index;return true;}
  void TrayResult(){if(!Visible&&!demo)tray.ShowBalloonTip(3000,"ScreenDock",status.Text,ToolTipIcon.Info);}
  void BuildTrayMenu(ContextMenuStrip menu){
   while(menu.Items.Count>0){var item=menu.Items[0];menu.Items.RemoveAt(0);item.Dispose();}
   menu.Items.Add("打开屏幕管理",null,delegate{ShowWindow();});menu.Items.Add(PresetMenu(true));menu.Items.Add(UpdateMenuItem());menu.Items.Add(new ToolStripSeparator());
   if(displays!=null)foreach(var d in displays){
    var target=d;var screen=new ToolStripMenuItem("屏幕 "+d.Number+" · "+d.Name+(d.Active?"":"（已停用）"));menu.Items.Add(screen);
    screen.DropDownItems.Add("打开此屏幕设置",null,delegate{if(PickTrayDisplay(target))ShowWindow();});
    var connection=new ToolStripMenuItem(d.Primary?"主屏受保护":d.Active?"停用此屏幕":"启用此屏幕"){Enabled=!busy&&!detecting&&brightnessPending.Count==0&&!d.Primary};
    connection.Click+=delegate{if(PickTrayDisplay(target)){ShowWindow();Toggle();}};screen.DropDownItems.Add(connection);
    int value;bool known=brightnessValues.TryGetValue(BrightnessKey(d),out value);if(demo){known=true;value=70;}
    var brightness=new ToolStripMenuItem(known?"亮度 · "+value+"%":"亮度 · 尚不可用"){Enabled=d.Active&&known&&!busy&&!BrightnessPending(d)};screen.DropDownItems.Add(brightness);
    foreach(int step in new[]{-10,10}){int delta=step;brightness.DropDownItems.Add(step<0?"降低 10%":"提高 10%",null,async delegate{if(!PickTrayDisplay(target))return;int current;if(!brightnessValues.TryGetValue(BrightnessKey(target),out current)){if(!demo)return;current=70;}await AdjustBrightness(Math.Max(0,Math.Min(100,current+delta)));TrayResult();});}
    brightness.DropDownItems.Add(new ToolStripSeparator());foreach(int level in new[]{25,50,75,100}){int percent=level;var choice=new ToolStripMenuItem(level+"%"){Checked=known&&value==level};choice.Click+=async delegate{if(PickTrayDisplay(target)){await AdjustBrightness(percent);TrayResult();}};brightness.DropDownItems.Add(choice);}
    ModeInfo info;modeInfos.TryGetValue(BrightnessKey(d),out info);bool eyeOn=EyeOn(d);
    var eye=new ToolStripMenuItem(eyeOn?"关闭护眼并恢复":"开启护眼模式"){Checked=eyeOn,Enabled=d.Active&&!busy&&!BrightnessPending(d)&&!ModesPending(d)&&(eyeOn||EyeComfort.Choices(info).Count>0)};
    eye.Click+=async delegate{if(PickTrayDisplay(target)){await ChangeEye(true);TrayResult();}};screen.DropDownItems.Add(eye);
   }
   menu.Items.Add(new ToolStripSeparator());menu.Items.Add("识别屏幕",null,delegate{Identify();});menu.Items.Add("刷新设备",null,delegate{RefreshDisplays(true);});
   var startup=new ToolStripMenuItem("开机启动");try{startup.Checked=StartupRegistration.Enabled();}catch(Exception ex){startup.Enabled=false;startup.ToolTipText=ex.Message;}
   startup.Click+=delegate{try{StartupRegistration.Set(!startup.Checked);startup.Checked=StartupRegistration.Enabled();if(!demo)tray.ShowBalloonTip(3000,"ScreenDock",startup.Checked?"已开启开机启动，下次登录后直接进入托盘。":"已关闭开机启动。",ToolTipIcon.Info);}catch(Exception ex){MessageBox.Show("无法修改开机启动："+ex.Message,"ScreenDock");}};menu.Items.Add(startup);
   menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出 ScreenDock",null,delegate{if(busy||brightnessPending.Count>0){ShowWindow();return;}quitting=true;Close();});
  }
 }
}
