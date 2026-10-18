using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ScreenDock {
 public static class EyeComfort {
  // Only standard warm presets explicitly advertised by this physical monitor.
  public static List<ModeOption> Choices(ModeInfo info){return info==null?new List<ModeOption>():info.Options.Where(x=>x.Code==0x14&&(x.Value==4||x.Value==3)).OrderByDescending(x=>x.Value).ToList();}
  public static ModeOption Choose(ModeInfo info,int strength){var options=Choices(info);if(options.Count==0)throw new InvalidOperationException("此显示器未提供可用的暖色预设。");return options[(int)Math.Round(Math.Max(0,Math.Min(100,strength))*(options.Count-1)/100.0,MidpointRounding.AwayFromZero)];}
  public static string PathFor(Display d){return MonitorModes.RecoveryPath(d).Replace("mode-recovery-","eye-comfort-");}
  public static bool HasBackup(Display d){return d!=null&&File.Exists(PathFor(d));}
  static void Save(Display d,ModeSnapshot snapshot){string path=PathFor(d);Directory.CreateDirectory(Path.GetDirectoryName(path));string pending=path+".tmp";using(var stream=File.Create(pending))new XmlSerializer(typeof(ModeSnapshot)).Serialize(stream,snapshot);File.Move(pending,path);}
  public static void Restore(Display d){ModeSnapshot snapshot;using(var stream=File.OpenRead(PathFor(d)))snapshot=(ModeSnapshot)new XmlSerializer(typeof(ModeSnapshot)).Deserialize(stream);MonitorModes.Restore(d,snapshot);File.Delete(PathFor(d));}
  public static void Apply(Display d,int strength){
   var info=MonitorModes.Detect(d);var option=Choose(info,strength);bool first=!HasBackup(d);
   if(first)Save(d,MonitorModes.Capture(d,option));
   try{MonitorModes.WithMonitor(d,h=>{MonitorModes.SetChecked(h,option.Code,option.Value);return true;});}
   catch(Exception ex){if(first){try{Restore(d);}catch(Exception restore){throw new Exception(ex.Message+" 恢复失败，备份已保留："+restore.Message);}}throw;}
  }
 }
 public partial class MainForm {
  Button eyeToggle;BrightnessSlider eyeSlider=new BrightnessSlider();Label eyeHint=new Label(),eyeValue=new Label();
  Dictionary<string,int> eyeStrength=new Dictionary<string,int>();HashSet<string> demoEye=new HashSet<string>();
  bool EyeOn(Display d){return d!=null&&(demo?demoEye.Contains(BrightnessKey(d)):EyeComfort.HasBackup(d));}
  void AddEyeControls(){
   Rule(300,652,834,1);LabelAt(this,"护眼模式",306,669,180,30,17,true,Art.Ink);
   eyeSlider.SetBounds(650,670,235,34);Controls.Add(eyeSlider);
   eyeValue.SetBounds(895,671,90,30);eyeValue.Font=Art.Font(14);Controls.Add(eyeValue);
   eyeToggle=Button("开启",async delegate{await ChangeEye(true);});eyeToggle.SetBounds(998,668,136,39);Controls.Add(eyeToggle);
   eyeHint.SetBounds(306,712,820,28);eyeHint.Font=Art.Font(12);eyeHint.ForeColor=Art.Muted;Controls.Add(eyeHint);
   eyeSlider.ValueChanged+=delegate{var d=Current;if(d==null)return;ModeInfo info;modeInfos.TryGetValue(BrightnessKey(d),out info);if(EyeComfort.Choices(info).Count>0)eyeValue.Text=EyeComfort.Choose(info,eyeSlider.Value).Value==3?"4000 K":"5000 K";};
   eyeSlider.Committed+=async delegate{var d=Current;if(d==null||busy||BrightnessPending(d))return;eyeStrength[BrightnessKey(d)]=eyeSlider.Value;if(EyeOn(d))await ChangeEye(false);};
  }
  void UpdateEyeUi(){if(eyeToggle==null)return;var d=Current;ModeInfo info=null;string key=d==null?"":BrightnessKey(d);modeInfos.TryGetValue(key,out info);bool on=EyeOn(d),supported=EyeComfort.Choices(info).Count>0;
   eyeToggle.Text=on?"关闭并恢复":"开启";eyeToggle.Enabled=d!=null&&d.Active&&!busy&&!BrightnessPending(d)&&!ModesPending(d)&&(on||supported);
   int strength;eyeSlider.Value=eyeStrength.TryGetValue(key,out strength)?strength:50;
   eyeSlider.Enabled=d!=null&&d.Active&&!busy&&!BrightnessPending(d)&&!ModesPending(d)&&EyeComfort.Choices(info).Count>1;
   eyeValue.Text=supported?(EyeComfort.Choose(info,eyeSlider.Value).Value==3?"4000 K":"5000 K"):"—";
   eyeHint.Text=d==null?"请选择显示器":!d.Active?"请先启用此屏幕。":on?"已应用暖色 / 有恢复备份 · 关闭后恢复开启前设置。":supported?"仅调整此屏幕 · 暖色档位按硬件支持切换。":info==null&&!modeErrors.ContainsKey(key)?"正在检测暖色预设…":"不支持：未检测到可用的暖色预设。";
   eyeHint.ForeColor=on?Color.FromArgb(35,145,99):!supported&&(info!=null||modeErrors.ContainsKey(key))?Color.FromArgb(200,55,55):Art.Muted;
  }
  async Task ChangeEye(bool toggle){var d=Current;if(d==null||!d.Active||busy||BrightnessPending(d)||ModesPending(d))return;string key=BrightnessKey(d);bool restore=toggle&&EyeOn(d);int strength=eyeSlider.Value;eyeStrength[key]=strength;
   if(demo){if(restore)demoEye.Remove(key);else demoEye.Add(key);SelectDisplay();return;}
   busy=true;SelectDisplay();status.Text=restore?"正在恢复此屏幕的原设置…":"正在应用此屏幕的暖色预设…";
   string result;try{await Task.Run(()=>{if(restore)EyeComfort.Restore(d);else EyeComfort.Apply(d,strength);});result=restore?"已恢复此屏幕开启前的可读取设置。":"已向此屏幕应用暖色预设。";}catch(Exception ex){result=ex.Message;}
   finally{busy=false;}
   if(!IsDisposed){RefreshDisplays();status.Text=result;}
  }
 }
}
