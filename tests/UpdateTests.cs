using System;using System.IO;using System.Security.Cryptography;using ScreenDock;
class UpdateTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Reject(Action action){try{action();}catch{return;}throw new Exception("Invalid update accepted");}
 static void Main(){
  Check(Updates.VersionOf("v1.10.0")>Updates.VersionOf("v1.9.0"),"lexical version comparison");
  Check(Updates.VersionOf("v1.1.0")==new Version(1,1,0,0),"revision normalization");Reject(()=>Updates.VersionOf("v1.1.0-preview"));
  var a=new ReleaseAsset{name="ScreenDock-Setup-x64.exe",size=100,digest="sha256:"+new string('a',64),browser_download_url="https://github.com/penghanrose-coder/ScreenDock/releases/download/v1.1.0/ScreenDock-Setup-x64.exe"};
  var r=new UpdateRelease{tag_name="v1.1.0",assets=new[]{a}};Check(Updates.Asset(r)==a,"valid asset rejected");
  r.prerelease=true;Reject(()=>Updates.Asset(r));r.prerelease=false;r.draft=true;Reject(()=>Updates.Asset(r));r.draft=false;
  a.browser_download_url="https://example.com/Setup.exe";Reject(()=>Updates.Asset(r));a.browser_download_url="https://github.com/penghanrose-coder/ScreenDock/releases/download/v1.1.0/ScreenDock-Setup-x64.exe";
  a.size=1024L*1024*129;Reject(()=>Updates.Asset(r));a.size=100;a.digest=null;Reject(()=>Updates.Asset(r));
  var path=Path.GetTempFileName();try{File.WriteAllText(path,"test");string hash;using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");Check(Updates.Verify(path,hash),"hash mismatch");File.AppendAllText(path,"corrupt");Check(!Updates.Verify(path,hash),"corruption accepted");}finally{File.Delete(path);}
  Reject(()=>Updates.Quote("bad\"path"));Check(Updates.Quote(@"D:\Program Files\ScreenDock")=="\"D:\\Program Files\\ScreenDock\"","path quoting");
  Console.WriteLine("PASS: version ordering, official URL, draft/prerelease, size, hash, tamper detection and path quoting.");
 }
}
