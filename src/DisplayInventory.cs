using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ScreenDock {
 public class ConnectedTarget {public string Path,Name,Source;public bool Active;}
 public static class DisplayInventory {
  [StructLayout(LayoutKind.Sequential)] public struct Luid {public uint Low;public int High;}
  [StructLayout(LayoutKind.Sequential)] public struct Source {public Luid Adapter;public uint Id,Mode,Status;}
  [StructLayout(LayoutKind.Sequential)] public struct Target {public Luid Adapter;public uint Id,Mode,Technology,Rotation,Scaling,Numerator,Denominator,Scanline;public int Available;public uint Status;}
  [StructLayout(LayoutKind.Sequential)] public struct PathInfo {public Source Source;public Target Target;public uint Flags;}
  [StructLayout(LayoutKind.Sequential)] public struct Header {public uint Type,Size;public Luid Adapter;public uint Id;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct TargetName {public Header Header;public uint Flags,Technology;public ushort Manufacturer,Product;public uint Connector;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string Friendly;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Path;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct SourceName {public Header Header;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;}
  [DllImport("user32.dll")]static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
  [DllImport("user32.dll")]static extern int QueryDisplayConfig(uint flags,ref uint paths,[Out]PathInfo[] pathArray,ref uint modes,IntPtr modeArray,IntPtr topology);
  [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]static extern int GetTargetName(ref TargetName name);
  [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]static extern int GetSourceName(ref SourceName name);
  public static Dictionary<string,ConnectedTarget> Read(){
   for(int attempt=0;attempt<3;attempt++){uint pathCount,modeCount;if(GetDisplayConfigBufferSizes(1,out pathCount,out modeCount)!=0)return null;if(pathCount>10000||modeCount>10000)return null;var paths=new PathInfo[pathCount];IntPtr modes=Marshal.AllocHGlobal(Math.Max(1,(int)modeCount)*64);
    try{int result=QueryDisplayConfig(1,ref pathCount,paths,ref modeCount,modes,IntPtr.Zero);if(result==122)continue;if(result!=0)return null;var found=new Dictionary<string,ConnectedTarget>(StringComparer.OrdinalIgnoreCase);
     for(int i=0;i<pathCount;i++){var p=paths[i];if(p.Target.Available==0)continue;var target=new TargetName{Header=new Header{Type=2,Size=(uint)Marshal.SizeOf(typeof(TargetName)),Adapter=p.Target.Adapter,Id=p.Target.Id}};if(GetTargetName(ref target)!=0||String.IsNullOrWhiteSpace(target.Path))continue;var source=new SourceName{Header=new Header{Type=1,Size=(uint)Marshal.SizeOf(typeof(SourceName)),Adapter=p.Source.Adapter,Id=p.Source.Id}};GetSourceName(ref source);bool active=(p.Flags&1)!=0;ConnectedTarget old;if(!found.TryGetValue(target.Path,out old)||active)found[target.Path]=new ConnectedTarget{Path=target.Path,Name=target.Friendly,Source=source.Name,Active=active};}
     return found;
    }finally{Marshal.FreeHGlobal(modes);}
   }return null;
  }
 }
}
