using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
namespace ScreenDock {
 public class ScreenPreset {public string Name;public List<PresetScreen> Screens=new List<PresetScreen>();}
 public class PresetScreen {public string Identity;public bool Active,Primary;public Native.Mode Mode;public int? Brightness;public bool? EyeEnabled;public int? WarmStrength;}
 public class PresetBook {public List<ScreenPreset> Items=new List<ScreenPreset>();}
 public static class Presets {
  public static string FilePath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenDock","presets.xml");}}
  public static PresetBook Load(){if(!File.Exists(FilePath))return new PresetBook();using(var stream=File.OpenRead(FilePath))return (PresetBook)new XmlSerializer(typeof(PresetBook)).Deserialize(stream);}
  public static void Save(PresetBook book){Directory.CreateDirectory(Path.GetDirectoryName(FilePath));string temp=FilePath+".tmp";using(var stream=File.Create(temp))new XmlSerializer(typeof(PresetBook)).Serialize(stream,book);if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);}
  public static Dictionary<string,Native.Mode> Plan(ScreenPreset preset,List<Display> current){
   if(preset==null||preset.Screens==null||preset.Screens.Count!=current.Count||current.Count==0)throw new Exception("连接的屏幕数量与预设不同，请使用原来的屏幕组合。");
   if(current.Any(d=>String.IsNullOrEmpty(d.Identity))||current.Select(d=>d.Identity).Distinct().Count()!=current.Count||preset.Screens.Select(d=>d.Identity).Distinct().Count()!=preset.Screens.Count)throw new Exception("无法唯一识别屏幕，已取消应用。");
   if(preset.Screens.Count(x=>x.Active&&x.Primary)!=1||current.Count(x=>x.Primary)!=1)throw new Exception("预设或当前主屏状态无效。");
   var result=new Dictionary<string,Native.Mode>();foreach(var item in preset.Screens){var d=current.Find(x=>x.Identity==item.Identity);if(d==null)throw new Exception("预设中的显示器未连接或设备标识已变化。");if(item.Primary!=d.Primary)throw new Exception("主显示器已变化，请先在 Windows 中恢复原主屏，或重新保存预设。");if(d.Primary&&!item.Active)throw new Exception("不能停用主显示器。");if(item.Active&&(item.Mode.width==0||item.Mode.height==0))throw new Exception("预设分辨率无效。");if(item.WarmStrength.HasValue&&(item.WarmStrength<0||item.WarmStrength>100))throw new Exception("预设暖色强度无效。");if(item.Brightness.HasValue&&(item.Brightness<0||item.Brightness>100))throw new Exception("预设亮度无效。");var mode=item.Active?item.Mode:Backend.Disabled(d.Mode);mode.device=d.Mode.device;mode.extra=0;result.Add(d.Device,mode);}return result;
  }
 }
 public partial class MainForm {
  public static void ApplyPresetSettings(PresetScreen saved,Func<bool> hasBackup,Action<int> warm,Action restore,Action<int> brightness){
   if(saved.EyeEnabled.HasValue){if(saved.EyeEnabled.Value)warm(saved.WarmStrength??50);else if(hasBackup())restore();}
   if(saved.Brightness.HasValue)brightness(saved.Brightness.Value);
  }
  bool PresetReady {get{return !busy&&!detecting&&brightnessPending.Count==0;}}
  string PresetName(string title,string initial){using(var dialog=new Form{Text=title,ClientSize=new Size(420,155),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,Font=Art.Font(15)}){
   var label=new Label{Text="预设名称（例如：办公双屏、游戏单屏）",Bounds=new Rectangle(18,15,390,28)};var input=new TextBox{Text=initial??"",MaxLength=40,Bounds=new Rectangle(18,51,380,30)};var ok=new Button{Text="保存",DialogResult=DialogResult.OK,Bounds=new Rectangle(218,105,85,32)};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(313,105,85,32)};dialog.Controls.AddRange(new Control[]{label,input,ok,cancel});dialog.AcceptButton=ok;dialog.CancelButton=cancel;return dialog.ShowDialog(this)==DialogResult.OK?input.Text.Trim():null;}}
  ToolStripMenuItem PresetMenu(bool quick=false){var menu=new ToolStripMenuItem("屏幕预设");menu.DropDownOpening+=delegate{FillPresets(menu,quick);};menu.DropDownItems.Add("加载…");return menu;}
  void FillPresets(ToolStripMenuItem menu,bool quick=false){while(menu.DropDownItems.Count>0){var old=menu.DropDownItems[0];menu.DropDownItems.RemoveAt(0);old.Dispose();}
   var save=new ToolStripMenuItem("保存当前状态为预设…",null,async delegate{ShowWindow();await SavePreset();}){Enabled=PresetReady&&!demo};menu.DropDownItems.Add(save);menu.DropDownItems.Add(new ToolStripSeparator());
   try{var book=Presets.Load();if(book.Items.Count==0)menu.DropDownItems.Add(new ToolStripMenuItem("暂无预设，先保存当前状态"){Enabled=false});foreach(var p in book.Items){var preset=p;if(quick){var shortcut=new ToolStripMenuItem(p.Name,null,async delegate{await RunPresetCommand(preset);});shortcut.ToolTipText="点击应用此预设";menu.DropDownItems.Add(shortcut);continue;}var item=new ToolStripMenuItem(p.Name){ToolTipText="保存的布局、亮度和护眼状态"};menu.DropDownItems.Add(item);item.DropDownItems.Add(new ToolStripMenuItem("应用",null,async delegate{await RunPresetCommand(preset);}){Enabled=PresetReady&&!demo});item.DropDownItems.Add("重命名…",null,delegate{EditPreset(preset.Name,false);});item.DropDownItems.Add("删除…",null,delegate{EditPreset(preset.Name,true);});}}
   catch(Exception ex){menu.DropDownItems.Add(new ToolStripMenuItem("预设读取失败"){Enabled=false,ToolTipText=ex.Message});status.Text=ex.Message;}
  }
  async Task RunPresetCommand(ScreenPreset preset){
   if(!PresetReady){status.Text="正在检测或调节显示器，请稍后再切换预设。";if(!Visible&&!demo)tray.ShowBalloonTip(4000,"屏幕预设",status.Text,ToolTipIcon.Info);else MessageBox.Show(this,status.Text,"屏幕预设");return;}
   if(demo){status.Text="演示模式：已收到预设切换操作，未修改真实屏幕。";return;}
   status.Text="正在应用预设“"+preset.Name+"”…";await ApplyPreset(preset);TrayResult();
  }
  void EditPreset(string name,bool remove){ShowWindow();try{var book=Presets.Load();var item=book.Items.Find(x=>x.Name==name);if(item==null)return;if(remove){if(MessageBox.Show(this,"删除预设“"+name+"”？","屏幕预设",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;book.Items.Remove(item);}else{string next=PresetName("重命名预设",name);if(String.IsNullOrWhiteSpace(next))return;if(book.Items.Any(x=>x!=item&&String.Equals(x.Name,next,StringComparison.OrdinalIgnoreCase)))throw new Exception("已有同名预设。");item.Name=next;}Presets.Save(book);}catch(Exception ex){MessageBox.Show(this,ex.Message,"屏幕预设");}}
  async Task SavePreset(){if(!PresetReady||demo)return;string name=PresetName("保存屏幕预设","");if(String.IsNullOrWhiteSpace(name)||!PresetReady)return;busy=true;SelectDisplay();try{var book=Presets.Load();var existing=book.Items.Find(x=>String.Equals(x.Name,name,StringComparison.OrdinalIgnoreCase));if(existing!=null&&MessageBox.Show(this,"用当前屏幕状态更新预设“"+name+"”？","更新预设",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;var preset=await Task.Run(()=>{var snapshot=Backend.Enumerate();var p=new ScreenPreset{Name=name};foreach(var d in snapshot){var row=new PresetScreen{Identity=d.Identity,Active=d.Active,Primary=d.Primary,Mode=d.Mode};if(d.Active)try{row.Brightness=Brightness.Access(d.Device,d.Identity,null).Percent;}catch(Exception){}if(d.Active){row.EyeEnabled=EyeComfort.HasBackup(d);if(row.EyeEnabled==true){var info=MonitorModes.Detect(d);uint current;if(!info.Current.TryGetValue(0x14,out current)||(current!=3&&current!=4))throw new Exception("护眼备份存在，但当前不是暖色档位；请先关闭护眼并重新开启后保存。");row.WarmStrength=current==3?100:0;}}p.Screens.Add(row);}Presets.Plan(p,Backend.Enumerate());return p;});if(existing!=null)book.Items.Remove(existing);book.Items.Add(preset);Presets.Save(book);int missing=preset.Screens.Count(x=>x.Active&&!x.Brightness.HasValue);status.Text="已保存预设“"+name+"”"+(missing>0?"；"+missing+" 块屏幕亮度不可读取，未保存亮度。":"，包含布局、亮度和护眼状态。");}catch(Exception ex){MessageBox.Show(this,ex.Message,"保存预设失败");}finally{busy=false;SelectDisplay();}}
  async Task ApplyPreset(ScreenPreset preset){if(!PresetReady||demo)return;busy=true;SelectDisplay();Dictionary<string,Native.Mode> before=null;bool attempted=false,accepted=false;string message="";
   try{var current=Backend.Enumerate();var plan=Presets.Plan(preset,current);before=current.ToDictionary(d=>d.Device,d=>d.Active?d.Mode:Backend.Disabled(d.Mode));bool layoutChanged=current.Any(d=>{var m=plan[d.Device];return d.Active!=(m.width>0&&m.height>0)||(d.Active&&(d.Mode.x!=m.x||d.Mode.y!=m.y||d.Mode.width!=m.width||d.Mode.height!=m.height||d.Mode.frequency!=m.frequency||d.Mode.orientation!=m.orientation));});if(layoutChanged){Backend.Remember(current);attempted=true;Backend.Apply(plan);
    using(var confirm=new Confirm("已应用预设布局，是否保留？")){if((Visible?confirm.ShowDialog(this):confirm.ShowDialog())!=DialogResult.OK){Backend.Apply(before);attempted=false;message="已恢复原布局，未修改亮度。";}else accepted=true;}}else accepted=true;
    if(accepted){attempted=false;var failures=new List<string>();var active=await Task.Run(()=>Backend.Enumerate());await Task.Run(()=>{Parallel.ForEach(preset.Screens.Where(x=>x.Active),new ParallelOptions{MaxDegreeOfParallelism=2},saved=>{var d=active.Find(x=>x.Identity==saved.Identity&&x.Active);if(d==null){lock(failures)failures.Add("屏幕未就绪");return;}try{ApplyPresetSettings(saved,()=>EyeComfort.HasBackup(d),strength=>EyeComfort.Apply(d,strength),()=>EyeComfort.Restore(d),value=>Brightness.Access(d.Device,d.Identity,value));}catch(Exception ex){lock(failures)failures.Add(d.Name+"："+ex.Message);}});});message=failures.Count==0?"已应用预设“"+preset.Name+"”。":"布局已保留，部分护眼或亮度设置未应用："+String.Join("；",failures);if(failures.Count>0&&Visible)MessageBox.Show(this,message,"屏幕预设");}
   }catch(Exception ex){message=ex.Message;if(attempted&&before!=null)try{Backend.Apply(before);message+=" 已恢复原布局。";}catch(Exception rollback){message+=" 恢复失败："+rollback.Message;}if(Visible)MessageBox.Show(this,message,"预设未能完整应用");}
   finally{busy=false;RefreshDisplays();status.Text=message;}
  }
 }
}
