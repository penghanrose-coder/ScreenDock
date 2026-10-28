using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenDock {
 public class ReleaseAsset { public string name, browser_download_url, digest; public long size; }
 public class UpdateRelease { public string tag_name, body; public bool draft, prerelease; public ReleaseAsset[] assets; }
 public class UpdateJob { public int Parent; public string Installer, Hash, Target; public bool Tray; }
 public static class Updates {
  const string Api="https://api.github.com/repos/penghanrose-coder/ScreenDock/releases/latest";
  const string Repo="https://github.com/penghanrose-coder/ScreenDock/releases/download/";
  public static string DataDir {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenDock");}}
  public static Version Current {get{return Assembly.GetExecutingAssembly().GetName().Version;}}
  public static bool Automatic {get{return !File.Exists(Path.Combine(DataDir,"updates-disabled"));}set{Directory.CreateDirectory(DataDir);var p=Path.Combine(DataDir,"updates-disabled");if(value){if(File.Exists(p))File.Delete(p);}else File.WriteAllText(p,"disabled");}}
  public static Version VersionOf(string tag){if(tag==null||!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$"))throw new Exception("版本号格式无效。");var v=new Version(tag.Substring(1));return new Version(v.Major,v.Minor,v.Build,0);}
  public static ReleaseAsset Asset(UpdateRelease r){
   if(r==null||r.draft||r.prerelease)throw new Exception("不是正式发布版本。");VersionOf(r.tag_name);
   if(r.assets!=null)foreach(var a in r.assets)if(a.name=="ScreenDock-Setup-x64.exe"){
    if(a.browser_download_url!=Repo+r.tag_name+"/ScreenDock-Setup-x64.exe"||a.size<=0||a.size>128*1024*1024||a.digest==null||!Regex.IsMatch(a.digest,@"^sha256:[a-fA-F0-9]{64}$"))throw new Exception("安装包地址或校验信息无效。");return a;
   }throw new Exception("此版本尚未提供安装包。");
  }
  static HttpWebRequest Request(string url){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var r=(HttpWebRequest)WebRequest.Create(url);r.UserAgent="ScreenDock/"+Current;r.Timeout=20000;r.ReadWriteTimeout=20000;return r;}
  public static UpdateRelease Check(){using(var r=Request(Api).GetResponse())using(var stream=r.GetResponseStream())using(var reader=new StreamReader(stream)){var json=reader.ReadToEnd();var release=new JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<UpdateRelease>(json);Asset(release);return release;}}
  public static bool Verify(string path,string hash){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return String.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),hash,StringComparison.OrdinalIgnoreCase);}
  public static string Download(UpdateRelease release,Action<int> progress){
   var a=Asset(release);var dir=Path.Combine(DataDir,"Updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var path=Path.Combine(dir,"Setup.exe");
   try{using(var response=Request(a.browser_download_url).GetResponse())using(var input=response.GetResponseStream())using(var output=File.Create(path)){var buffer=new byte[65536];long total=0;int count;while((count=input.Read(buffer,0,buffer.Length))>0){total+=count;if(total>a.size)throw new Exception("安装包大小与发布信息不符。");output.Write(buffer,0,count);progress((int)(total*100/a.size));}if(total!=a.size)throw new Exception("下载不完整，请重试。");}if(!Verify(path,a.digest.Substring(7)))throw new Exception("安装包校验失败，请重试。");return path;}catch{if(File.Exists(path))File.Delete(path);throw;}
  }
  public static bool Installed(string executable){using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{26C69994-C754-4314-820D-3E207BEAAB68}_is1")){var dir=key==null?null:key.GetValue("InstallLocation") as string;return !String.IsNullOrEmpty(dir)&&String.Equals(Path.GetFullPath(Path.Combine(dir,"ScreenDock.exe")),Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase);}}
  public static string Quote(string s){if(s==null||s.IndexOfAny(new[]{'"','\r','\n'})>=0)throw new Exception("无效路径。");return "\""+s+"\"";}
  public static void Launch(string installer,string hash,bool tray){
   var dir=Path.GetDirectoryName(installer);var helper=Path.Combine(dir,"UpdateHelper.exe");File.Copy(Application.ExecutablePath,helper,true);
   var job=new UpdateJob{Parent=Process.GetCurrentProcess().Id,Installer=installer,Hash=hash,Target=Application.ExecutablePath,Tray=tray};var path=Path.Combine(dir,"job.json");File.WriteAllText(path,new JavaScriptSerializer().Serialize(job));
   Process.Start(new ProcessStartInfo(helper,"--finish-update "+Quote(path)){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir});
  }
  public static void Finish(string jobPath){
   UpdateJob job=null;
   try{job=new JavaScriptSerializer().Deserialize<UpdateJob>(File.ReadAllText(jobPath));if(!Installed(job.Target)||!Verify(job.Installer,job.Hash))throw new Exception("安装路径或文件校验不通过。");
    try{using(var parent=Process.GetProcessById(job.Parent)){if(!parent.WaitForExit(30000))throw new Exception("软件尚未退出，已取消更新。");}}catch(ArgumentException){}
    var args="/VERYSILENT /SUPPRESSMSGBOXES /SP- /NORESTART /DIR="+Quote(Path.GetDirectoryName(job.Target))+" /LOG="+Quote(Path.Combine(Path.GetDirectoryName(jobPath),"install.log"));
    using(var install=Process.Start(new ProcessStartInfo(job.Installer,args){UseShellExecute=true})){install.WaitForExit();if(install.ExitCode!=0)throw new Exception("安装未完成（"+install.ExitCode+"）。可重新打开软件后重试。");}
    Process.Start(new ProcessStartInfo(job.Target,job.Tray?"--tray":""){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(job.Target)});
    File.Delete(job.Installer);File.Delete(jobPath);
   }catch(Exception ex){MessageBox.Show("更新失败："+ex.Message,"ScreenDock 更新");if(job!=null&&File.Exists(job.Target))Process.Start(job.Target);}
  }
 }
 public partial class MainForm {
  bool checkingUpdate;string notifiedVersion;System.Windows.Forms.Timer updateTimer;
  void StartUpdateChecks(){if(demo)return;updateTimer=new System.Windows.Forms.Timer{Interval=15000};updateTimer.Tick+=async delegate{updateTimer.Interval=6*60*60*1000;if(Updates.Automatic)await CheckUpdates(false);};updateTimer.Start();}
  ToolStripMenuItem UpdateMenuItem(){return new ToolStripMenuItem("检查更新…",null,async delegate{await CheckUpdates(true);});}
  ToolStripMenuItem AutoUpdateItem(){var item=new ToolStripMenuItem("自动检查更新"){Checked=Updates.Automatic};item.Click+=delegate{try{Updates.Automatic=!Updates.Automatic;item.Checked=Updates.Automatic;}catch(Exception ex){MessageBox.Show(ex.Message,"更新设置");}};return item;}
  async Task CheckUpdates(bool manual){
   if(checkingUpdate||demo){if(manual)MessageBox.Show("正在检查或下载更新，请稍候。","ScreenDock 更新");return;}checkingUpdate=true;
   try{var release=await Task.Run(()=>Updates.Check());if(IsDisposed)return;var version=Updates.VersionOf(release.tag_name);
    if(version<=Updates.Current){if(manual)MessageBox.Show("当前已是最新版本："+Updates.Current.ToString(3),"ScreenDock 更新");return;}
    if(!manual){if(notifiedVersion!=release.tag_name){notifiedVersion=release.tag_name;tray.ShowBalloonTip(6000,"发现新版本 "+release.tag_name,"从托盘或帮助菜单选择“检查更新”，即可下载并安装。",ToolTipIcon.Info);}return;}
    var notes=release.body??"";if(notes.Length>900)notes=notes.Substring(0,900)+"…";
    if(MessageBox.Show("当前版本："+Updates.Current.ToString(3)+"\n新版本："+release.tag_name+"\n\n"+notes+"\n\n现在下载并安装？完成后将重启 ScreenDock，保留用户配置。","ScreenDock 更新",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
    if(!Updates.Installed(Application.ExecutablePath)){MessageBox.Show("当前为便携版。请从发布页下载安装版后使用自动安装更新。","ScreenDock 更新");Process.Start("https://github.com/penghanrose-coder/ScreenDock/releases/latest");return;}
    var progress=new Progress<int>(p=>{if(!IsDisposed)status.Text="正在下载更新："+p+"%";});
    var path=await Task.Run(()=>Updates.Download(release,p=>((IProgress<int>)progress).Report(p)));if(IsDisposed)return;
    if(busy||detecting||brightnessPending.Count>0||layoutDirty){MessageBox.Show("安装包已下载。请先完成当前调节或应用/重置未保存的布局，再检查更新安装。","ScreenDock 更新");return;}
    Updates.Launch(path,Updates.Asset(release).digest.Substring(7),!Visible);quitting=true;Close();
   }catch(Exception ex){if(!IsDisposed){if(manual)MessageBox.Show("暂时无法完成更新："+ex.Message+"\n旧版本仍可正常使用。","ScreenDock 更新");else status.Text="自动检查更新暂不可用，可稍后手动检查。";}}
   finally{checkingUpdate=false;}
  }
 }
}
