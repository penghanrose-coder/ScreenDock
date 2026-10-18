using System;using System.Collections.Generic;
namespace ScreenDock {
 public static class MonitorIo {
  static readonly Dictionary<string,object> gates=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
  public static T Run<T>(string device,Func<T> action){object gate;lock(gates){if(!gates.TryGetValue(device,out gate)){gate=new object();gates[device]=gate;}}lock(gate)return action();}
 }
}
