using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using System.Management;

namespace ScreenDock {
 public class ModeOption {public byte Code;public uint Value;public string Label;public override string ToString(){return Label+"  ["+Code.ToString("X2")+":"+Value.ToString("X2")+"]";}}
 public class ModeInfo {
  public string Hardware,Model,Capabilities,Note;
  public List<ModeOption> Options=new List<ModeOption>();public Dictionary<byte,uint> Current=new Dictionary<byte,uint>();
 }
 [XmlRoot("MonitorProfiles")] public class MonitorProfiles {[XmlElement("Profile")]public List<MonitorProfile> Profiles=new List<MonitorProfile>();}
 public class MonitorProfile {[XmlAttribute("hardware")]public string Hardware;[XmlAttribute("model")]public string Model;[XmlElement("Value")]public List<ModeAlias> Values=new List<ModeAlias>();}
 public class ModeAlias {[XmlAttribute("code")]public string Code;[XmlAttribute("value")]public string Value;[XmlAttribute("label")]public string Label;}
 public class ModeSnapshot {public string Device,Identity;public List<ModeSetting> Settings=new List<ModeSetting>();}
 public class ModeSetting {public byte Code;public uint Value;}
 public static class MonitorModes {
  class CachedCaps {public string Text;public DateTime Time;}
  static readonly Dictionary<string,CachedCaps> capsCache=new Dictionary<string,CachedCaps>();
  public static void ClearCache(){lock(capsCache)capsCache.Clear();}
  static string Capabilities(IntPtr handle,Display display){
   string key=display.Device+"|"+display.Identity;CachedCaps cached;
   lock(capsCache){if(capsCache.TryGetValue(key,out cached)&&DateTime.UtcNow-cached.Time<TimeSpan.FromMinutes(5))return cached.Text;}
   uint length;if(!GetCapabilitiesStringLength(handle,out length)||length<2||length>65536)throw new Exception("显示器没有返回可用的 DDC/CI 能力列表。");
   var text=new StringBuilder((int)length);if(!CapabilitiesRequestAndCapabilitiesReply(handle,text,length))throw new Exception("读取 DDC/CI 能力列表失败。");
   string value=text.ToString();Parse(value);lock(capsCache)capsCache[key]=new CachedCaps{Text=value,Time=DateTime.UtcNow};return value;
  }
  [DllImport("dxva2.dll",SetLastError=true)] static extern bool GetCapabilitiesStringLength(IntPtr monitor,out uint length);
  [DllImport("dxva2.dll",CharSet=CharSet.Ansi,SetLastError=true)] static extern bool CapabilitiesRequestAndCapabilitiesReply(IntPtr monitor,StringBuilder text,uint length);
  public static string Hardware(string identity){var parts=(identity??"").Split('\\');return parts.Length>1?parts[1]:"Unknown";}
  static string EdidModel(string hardware){try{using(var search=new ManagementObjectSearcher("root\\wmi","SELECT InstanceName, UserFriendlyName FROM WmiMonitorID"))using(var results=search.Get()){foreach(ManagementObject item in results)using(item){string name=(string)item["InstanceName"];if(name.StartsWith("DISPLAY\\"+hardware+"\\",StringComparison.OrdinalIgnoreCase)){var value=(ushort[])item["UserFriendlyName"];return new string(value.Where(x=>x!=0).Select(x=>(char)x).ToArray());}}}}catch(Exception){}return null;}
  static void ApplyProfile(ModeInfo info){string model=EdidModel(info.Hardware);if(!String.IsNullOrWhiteSpace(model))info.Model=model;string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"profiles","monitors.xml");if(!File.Exists(file))return;try{MonitorProfiles profiles;using(var stream=File.OpenRead(file))profiles=(MonitorProfiles)new XmlSerializer(typeof(MonitorProfiles)).Deserialize(stream);var profile=profiles.Profiles.FirstOrDefault(x=>String.Equals(x.Hardware,info.Hardware,StringComparison.OrdinalIgnoreCase));if(profile==null)return;if(!String.IsNullOrWhiteSpace(profile.Model))info.Model=profile.Model;foreach(var alias in profile.Values){byte code;uint value;if(Byte.TryParse(alias.Code,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out code)&&UInt32.TryParse(alias.Value,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value)&&!String.IsNullOrWhiteSpace(alias.Label)){var option=info.Options.Find(x=>x.Code==code&&x.Value==value);if(option!=null)option.Label=alias.Label;}}}catch(Exception ex){info.Note+=" 型号配置未加载："+ex.Message;}}
  public static T WithMonitor<T>(Display display,Func<IntPtr,T> operation){return MonitorIo.Run(display.Device,()=>WithMonitorCore(display,operation));}
  static T WithMonitorCore<T>(Display display,Func<IntPtr,T> operation){
   if(!Native.HasMonitor(display.Device,display.Identity))throw new Exception("显示设备已变化，请刷新设备重试。");
   IntPtr found=IntPtr.Zero;Native.EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,delegate(IntPtr h,IntPtr dc,IntPtr rect,IntPtr data){var info=new Native.Info{size=Marshal.SizeOf(typeof(Native.Info))};if(Native.GetMonitorInfo(h,ref info)&&info.device==display.Device)found=h;return true;},IntPtr.Zero);
   uint count;if(found==IntPtr.Zero||!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(found,out count)||count!=1)throw new Exception("需要一对一连接的活动显示器；请使用扩展桌面。");
   var monitors=new Native.Physical[count];if(!Native.GetPhysicalMonitorsFromHMONITOR(found,count,monitors))throw new Exception("无法访问物理显示器。");
   try{return operation(monitors[0].handle);}finally{Native.DestroyPhysicalMonitors(count,monitors);}
  }
  public static string Block(string input,string name){
   var m=Regex.Match(input,@"(?<![a-zA-Z0-9_])"+Regex.Escape(name)+@"\s*\(",RegexOptions.IgnoreCase);if(!m.Success)return "";
   int start=m.Index+m.Length,depth=1;for(int i=start;i<input.Length;i++){if(input[i]=='(')depth++;else if(input[i]==')'&&--depth==0)return input.Substring(start,i-start);}throw new FormatException("显示器能力字符串不完整。");
  }
  public static Dictionary<byte,List<uint>> Parse(string caps){
   string vcp=Block(caps,"vcp");var result=new Dictionary<byte,List<uint>>();int i=0;
   while(i<vcp.Length){while(i<vcp.Length&&Char.IsWhiteSpace(vcp[i]))i++;if(i>=vcp.Length)break;int start=i;while(i<vcp.Length&&Uri.IsHexDigit(vcp[i]))i++;byte code=0;if(start==i)throw new FormatException("无效的 VCP 能力字段。");bool standard=Byte.TryParse(vcp.Substring(start,i-start),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out code);while(i<vcp.Length&&Char.IsWhiteSpace(vcp[i]))i++;var values=new List<uint>();if(i<vcp.Length&&vcp[i]=='('){i++;start=i;while(i<vcp.Length&&vcp[i]!=')'){if(vcp[i]=='(')throw new FormatException("不支持嵌套 VCP 参数。");i++;}if(i>=vcp.Length)throw new FormatException("VCP 参数不完整。");foreach(string token in vcp.Substring(start,i-start).Split((char[])null,StringSplitOptions.RemoveEmptyEntries)){uint value;if(!UInt32.TryParse(token,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value)||value>65535)throw new FormatException("无效的 VCP 参数。");values.Add(value);}i++;}if(standard)result[code]=values.Distinct().ToList();}
   return result;
  }
  public static string Label(byte code,uint value){
   if(code==0x14){string[] labels={"","sRGB","显示器原生色彩","4000 K","5000 K","6500 K","7500 K","8200 K","9300 K","10000 K","11500 K","用户色彩 1","用户色彩 2","用户色彩 3"};if(value>0&&value<labels.Length)return "色彩 · "+labels[value];}
   return (code==0xDC?"画面预设":"色彩预设")+" 0x"+value.ToString("X2");
  }
  static ModeInfo Read(IntPtr handle,Display display){
   string text=Capabilities(handle,display);
   var info=new ModeInfo{Hardware=Hardware(display.Identity),Capabilities=text.ToString()};info.Model=Block(info.Capabilities,"model");if(String.IsNullOrWhiteSpace(info.Model))info.Model=info.Hardware;
   var codes=Parse(info.Capabilities);
   foreach(byte code in new byte[]{0x14,0xDC}){List<uint> values;if(!codes.TryGetValue(code,out values)||values.Count==0)continue;uint type,current,max;if(!Native.GetVCPFeatureAndVCPFeatureReply(handle,code,out type,out current,out max))continue;info.Current[code]=current;foreach(uint value in values)info.Options.Add(new ModeOption{Code=code,Value=value,Label=Label(code,value)});}
   info.Note=info.Options.Count==0?"显示器未公开可读取的色彩/画面模式选项。":"仅列出显示器声明且能够读取的标准模式。";
   if(codes.ContainsKey(0xDC)&&!info.Current.ContainsKey(0xDC))info.Note+=" 画面模式虽有声明，但回读失败，已隐藏。";
   ApplyProfile(info);return info;
  }
  public static ModeInfo Detect(Display display){return WithMonitor(display,h=>Read(h,display));}
  public static uint Get(IntPtr h,byte code){uint type,current,max;if(!Native.GetVCPFeatureAndVCPFeatureReply(h,code,out type,out current,out max))throw new Exception("无法读取模式 0x"+code.ToString("X2"));return current;}
  public static void SetChecked(IntPtr h,byte code,uint value){if(!Native.SetVCPFeature(h,code,value))throw new Exception("显示器拒绝模式指令。");System.Threading.Thread.Sleep(400);uint actual=Get(h,code);if(actual!=value)throw new Exception("回读模式为 0x"+actual.ToString("X2")+"，与请求值不一致。");}
  public static ModeSnapshot Capture(Display d,ModeOption option){var info=Detect(d);if(!info.Options.Any(x=>x.Code==option.Code&&x.Value==option.Value))throw new Exception("显示器已不再声明此模式，已取消操作。");return WithMonitor(d,h=>{var snapshot=new ModeSnapshot{Device=d.Device,Identity=d.Identity};foreach(byte code in new byte[]{0xDC,0x14,0x10,0x12,0x16,0x18,0x1A}){uint type,current,max;if(Native.GetVCPFeatureAndVCPFeatureReply(h,code,out type,out current,out max))snapshot.Settings.Add(new ModeSetting{Code=code,Value=current});}if(!snapshot.Settings.Any(x=>x.Code==option.Code))throw new Exception("无法备份当前模式。");return snapshot;});}
  public static void Restore(Display d,ModeSnapshot snapshot){if(snapshot.Device!=d.Device||snapshot.Identity!=d.Identity)throw new Exception("恢复记录不属于当前设备。");WithMonitor(d,h=>{foreach(var item in snapshot.Settings){if(Get(h,item.Code)!=item.Value)SetChecked(h,item.Code,item.Value);}foreach(var item in snapshot.Settings)if(Get(h,item.Code)!=item.Value)throw new Exception("恢复后 VCP "+item.Code.ToString("X2")+" 与原值不一致，请用显示器菜单检查。");return true;});}
  public static string RecoveryPath(Display d){using(var hash=System.Security.Cryptography.SHA256.Create()){string key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(d.Identity??""))).Replace("-","").Substring(0,16);return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenDock","mode-recovery-"+Hardware(d.Identity)+"-"+key+".xml");}}
  public static void SaveRecovery(Display d,ModeSnapshot snapshot){string path=RecoveryPath(d);Directory.CreateDirectory(Path.GetDirectoryName(path));using(var stream=File.Create(path))new XmlSerializer(typeof(ModeSnapshot)).Serialize(stream,snapshot);}
  public static ModeSnapshot LoadRecovery(Display d){using(var stream=File.OpenRead(RecoveryPath(d)))return (ModeSnapshot)new XmlSerializer(typeof(ModeSnapshot)).Deserialize(stream);}
  public static string Report(Display d,ModeInfo info){return "设备："+d.Number+"\r\n硬件型号："+info.Hardware+"\r\n能力型号："+info.Model+"\r\n"+info.Note+"\r\n"+String.Join("\r\n",info.Current.Select(p=>"当前 VCP "+p.Key.ToString("X2")+" = "+p.Value.ToString("X2")))+"\r\n"+String.Join("\r\n",info.Options.Select(o=>o.ToString()));}
 }
}


