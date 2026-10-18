using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenDock {
 public class ModeDialog : Form {
  Display display;ModeInfo info;bool busy,demo;ListBox options=new ListBox();Label model=new Label(),current=new Label(),message=new Label();Button detect,apply,export,recover;
  public ModeDialog(Display d,bool preview=false){display=d;demo=preview;Text="ScreenDock · 内置模式";ClientSize=new Size(650,580);FormBorderStyle=FormBorderStyle.Sizable;MinimumSize=new Size(550,460);MaximizeBox=true;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;BackColor=Art.Background;ForeColor=Art.Ink;Font=Art.Font(13);Icon=Art.Icon();
   model.SetBounds(26,22,590,38);model.Font=Art.Font(23,true);model.Text="显示器内置模式";Controls.Add(model);
   current.SetBounds(28,70,590,58);current.ForeColor=Art.Muted;Controls.Add(current);
   detect=Make("检测支持的模式",28,140,180,38,async delegate{await Detect();});
   export=Make("导出诊断",224,140,140,38,delegate{Export();});
   options.SetBounds(28,201,590,150);options.BorderStyle=BorderStyle.None;options.BackColor=Color.White;options.IntegralHeight=false;options.Font=Art.Font(14);options.DrawMode=DrawMode.OwnerDrawFixed;options.ItemHeight=28;options.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();int index=e.Index>=0?e.Index:options.SelectedIndex;if(index>=0&&index<options.Items.Count)TextRenderer.DrawText(e.Graphics,options.Items[index].ToString(),options.Font,e.Bounds,Art.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};Controls.Add(options);
   apply=Make("试用所选模式",28,376,180,40,async delegate{await Apply();});((SoftButton)apply).Primary=true;
   recover=Make("恢复上次切换前",224,376,180,40,async delegate{await Recover();});
   message.SetBounds(28,440,590,120);message.ForeColor=Art.Muted;message.Text="将读取显示器公开的色彩 / 画面预设。\n模式名称不明确时显示原始编号。";Controls.Add(message);
   FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy&&e.CloseReason==CloseReason.UserClosing)e.Cancel=true;};
   UiZoom.Attach(this,false,false);UpdateControls();if(!demo)Shown+=async delegate{await Detect();};
  }
  Button Make(string text,int x,int y,int w,int h,EventHandler handler){var button=new SoftButton{Text=text,Bounds=new Rectangle(x,y,w,h)};button.Click+=handler;Controls.Add(button);return button;}
  void UpdateControls(){detect.Enabled=!busy;apply.Enabled=!busy&&info!=null&&info.Options.Count>0;options.Enabled=apply.Enabled;export.Enabled=!busy&&info!=null;recover.Enabled=!busy&&!demo&&File.Exists(MonitorModes.RecoveryPath(display));}
  void Present(){model.Text=info.Model;current.Text="硬件标识："+info.Hardware+"\n"+String.Join("   ·   ",info.Current.Select(x=>(x.Key==0x14?"当前色彩":"当前画面")+"："+MonitorModes.Label(x.Key,x.Value)));options.Items.Clear();foreach(var option in info.Options)options.Items.Add(option);if(options.Items.Count>0)options.SelectedIndex=Math.Max(0,info.Options.FindIndex(x=>info.Current.ContainsKey(x.Code)&&info.Current[x.Code]==x.Value));message.Text=info.Note+"\n试用后 15 秒内未保留，将尝试恢复切换前的可读取设置。";}
  async Task Detect(){if(busy||demo)return;busy=true;UpdateControls();message.Text="正在读取显示器能力，可能需要数秒…";try{info=await Task.Run(()=>MonitorModes.Detect(display));Present();}catch(Exception ex){info=null;options.Items.Clear();message.Text=ex.Message;}finally{busy=false;UpdateControls();}}
  async Task Apply(){var option=options.SelectedItem as ModeOption;if(option==null||busy||demo)return;busy=true;UpdateControls();ModeSnapshot backup=null;bool attempted=false;string failure=null;
   try{message.Text="正在备份当前设置…";backup=await Task.Run(()=>MonitorModes.Capture(display,option));MonitorModes.SaveRecovery(display,backup);attempted=true;await Task.Run(()=>MonitorModes.WithMonitor(display,h=>{MonitorModes.SetChecked(h,option.Code,option.Value);return true;}));
    using(var confirm=new Confirm("显示器模式已切换，是否保留？")){if(confirm.ShowDialog(this)!=DialogResult.OK){message.Text="正在恢复…";await Task.Run(()=>MonitorModes.Restore(display,backup));attempted=false;message.Text="已恢复切换前可读取的设置。";}else{attempted=false;message.Text="已保留模式；可点击“恢复上次切换前”撤销。";}}
   }catch(Exception ex){failure=ex.Message;} if(failure!=null){if(attempted&&backup!=null){try{await Task.Run(()=>MonitorModes.Restore(display,backup));failure+="\n已恢复切换前可读取的设置。";}catch(Exception restore){failure+="\n恢复失败："+restore.Message;}}message.Text=failure;}
   busy=false;info=null;options.Items.Clear();current.Text="请重新检测以刷新模式状态。";UpdateControls();
  }
  async Task Recover(){if(busy||demo)return;busy=true;UpdateControls();try{var snapshot=MonitorModes.LoadRecovery(display);await Task.Run(()=>MonitorModes.Restore(display,snapshot));message.Text="已恢复上次切换前可读取的设置。请重新检测。";info=null;options.Items.Clear();}catch(Exception ex){message.Text="恢复失败："+ex.Message;}finally{busy=false;UpdateControls();}}
  void Export(){if(info==null)return;using(var dialog=new SaveFileDialog{Filter="文本报告|*.txt",FileName="ScreenDock-"+info.Hardware+".txt"})if(dialog.ShowDialog(this)==DialogResult.OK){try{File.WriteAllText(dialog.FileName,MonitorModes.Report(display,info)+"\r\n\r\n此报告仅包含型号、标准模式列表和当前模式；不含序列号或完整设备实例路径。");message.Text="诊断报告已保存。";}catch(Exception ex){message.Text="保存失败："+ex.Message;}}}
  public void Screenshot(string path){info=new ModeInfo{Hardware="PHLC25B",Model="Philips 275M1RZN",Note="示例预览：色彩模式可用；画面模式回读失败，已隐藏。"};info.Current[0x14]=5;foreach(uint value in new uint[]{1,4,5,6,7,8,10,11})info.Options.Add(new ModeOption{Code=0x14,Value=value,Label=MonitorModes.Label(0x14,value)});Present();UpdateControls();Show();Application.DoEvents();using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(path);}Close();}
 }
}




