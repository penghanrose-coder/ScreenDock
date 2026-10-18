using System;
using System.Runtime.InteropServices;

namespace ScreenDock {
 public class BrightnessState {
  public uint Minimum,Current,Maximum;
  public bool Vcp;
  public int Percent {get{return (int)Math.Round((Current-Minimum)*100.0/(Maximum-Minimum));}}
 }
 public static class Brightness {
  [DllImport("dxva2.dll",SetLastError=true)] static extern bool GetMonitorBrightness(IntPtr handle,out uint minimum,out uint current,out uint maximum);
  [DllImport("dxva2.dll",SetLastError=true)] static extern bool SetMonitorBrightness(IntPtr handle,uint value);
  public static uint RawValue(uint minimum,uint maximum,int percent){if(maximum<=minimum||percent<0||percent>100)throw new ArgumentOutOfRangeException();return minimum+(uint)Math.Round((maximum-minimum)*(percent/100.0));}
  static BrightnessState Read(IntPtr handle){
   uint min,current,max,type;bool vcp=false;
   if(!GetMonitorBrightness(handle,out min,out current,out max)||max<=min||current<min||current>max){
    min=0;vcp=true;
    if(!Native.GetVCPFeatureAndVCPFeatureReply(handle,0x10,out type,out current,out max)||max==0||current>max)throw new Exception("未能读取亮度。请开启显示器的 DDC/CI；内置屏幕或当前连接可能不支持。");
   }
   return new BrightnessState{Minimum=min,Current=current,Maximum=max,Vcp=vcp};
  }
  public static BrightnessState Access(string device,string identity,int? percent){return MonitorIo.Run(device,()=>AccessCore(device,identity,percent));}
  static BrightnessState AccessCore(string device,string identity,int? percent){
   if(!Native.HasMonitor(device,identity))throw new Exception("显示设备已变化，请刷新设备后重试。");
   IntPtr found=IntPtr.Zero;
   Native.EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,delegate(IntPtr h,IntPtr dc,IntPtr rect,IntPtr data){var info=new Native.Info{size=Marshal.SizeOf(typeof(Native.Info))};if(Native.GetMonitorInfo(h,ref info)&&info.device==device)found=h;return true;},IntPtr.Zero);
   uint count;if(found==IntPtr.Zero||!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(found,out count)||count==0)throw new Exception("无法访问此显示器。请确认屏幕已启用且支持 DDC/CI。");
   // A cloned logical display can reference several panels. Do not write ambiguously.
   if(count!=1)throw new Exception("此显示设备对应多个物理屏幕，请切换到扩展桌面后调节亮度。");
   var physical=new Native.Physical[count];if(!Native.GetPhysicalMonitorsFromHMONITOR(found,count,physical))throw new Exception("无法建立显示器亮度连接。");
   try{
    var state=Read(physical[0].handle);
    if(percent.HasValue){uint value=RawValue(state.Minimum,state.Maximum,percent.Value);bool ok=state.Vcp?Native.SetVCPFeature(physical[0].handle,0x10,value):SetMonitorBrightness(physical[0].handle,value);if(!ok)throw new Exception("显示器拒绝亮度设置；HDR 或预设画面模式可能限制调节。");
     System.Threading.Thread.Sleep(120);
     try { state=Read(physical[0].handle); } catch(Exception){throw new Exception("亮度指令已发送，但未能回读实际亮度，请重新读取确认。");}
    }
    return state;
   }finally{Native.DestroyPhysicalMonitors(count,physical);}
  }
 }
}
