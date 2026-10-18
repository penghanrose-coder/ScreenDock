using System;
using ScreenDock;
public class ModeTests {
 public static void Main(){
  var parsed=MonitorModes.Parse("(model(MStar)vcp(10 14(02 05 06) DC( 51 00 04 ) E2A004(00 01 02) E2A006(00 01) F0(00 01)))");
  if(parsed[0x14].Count!=3||parsed[0xDC][0]!=0x51||parsed.ContainsKey(0xE2)||!parsed.ContainsKey(0xF0))throw new Exception("Vendor extension isolation failed");
  var lowercase=MonitorModes.Parse("(vcp(14(0b 0B) dc(00 04)))");if(lowercase[0x14].Count!=1||lowercase[0x14][0]!=11)throw new Exception("Hex case/dedup failed");
  foreach(var broken in new[]{"(vcp(14(01)","(vcp(14(zz)))","(vcp(14(100000)))"}){bool rejected=false;try{MonitorModes.Parse(broken);}catch(FormatException){rejected=true;}if(!rejected)throw new Exception("Malformed capability accepted");}
  if(MonitorModes.Parse("(model(Test))").Count!=0)throw new Exception("Absent vcp");
  if(MonitorModes.Hardware("MONITOR\\PHLC34D\\unique-instance")!="PHLC34D")throw new Exception("Hardware identifier");
  Console.WriteLine("PASS: extended vendor tokens, sparse mode values, case/dedup, malformed data rejection, empty capabilities, hardware identifier.");
 }
}
