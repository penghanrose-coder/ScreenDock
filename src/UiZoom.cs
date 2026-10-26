using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ScreenDock {
 public class ZoomCanvas : Panel { public float Zoom=1;public ZoomCanvas(){DoubleBuffered=true;} }
 public static class UiZoom {
  public static int Percent=ReadPreference();
  static string PreferencePath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenDock","ui-scale.txt");}}
  static int ReadPreference(){try{int value;if(Int32.TryParse(File.ReadAllText(PreferencePath),out value)&&value>=100&&value<=250)return value;}catch(IOException){}catch(UnauthorizedAccessException){}return 150;}
  public static float Factor(Control control){for(Control c=control;c!=null;c=c.Parent){var canvas=c as ZoomCanvas;if(canvas!=null)return canvas.Zoom;}return 1;}
  class Layout {public Control Control;public Rectangle Bounds;public Font Font,OwnedFont;public int ItemHeight;}
  static void Capture(Control parent,List<Layout> layouts){foreach(Control control in parent.Controls){var list=control as ListBox;layouts.Add(new Layout{Control=control,Bounds=control.Bounds,Font=control.Font,ItemHeight=list==null?0:list.ItemHeight});Capture(control,layouts);}}
  public static void Attach(Form form,bool toolbar,bool savePreference,bool responsive=false){
   form.AutoScaleMode=AutoScaleMode.None;Size original=form.ClientSize;
   var canvas=new ZoomCanvas{Size=original,BackColor=form.BackColor};var children=new List<Control>();foreach(Control c in form.Controls)if(!(c is MenuStrip))children.Add(c);foreach(var c in children){form.Controls.Remove(c);canvas.Controls.Add(c);}
   var viewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=form.BackColor};viewport.Controls.Add(canvas);form.Controls.Add(viewport);if(form.MainMenuStrip!=null){form.MainMenuStrip.Dock=DockStyle.Top;viewport.BringToFront();}
   var layouts=new List<Layout>();Capture(canvas,layouts);
   // Rebuild from the original rectangles so maximize/restore never accumulates rounding drift.
   bool arranging=false;
   Action fit=delegate{
    if(!responsive||arranging||form.IsDisposed||form.WindowState==FormWindowState.Minimized)return;arranging=true;
    try{float scale=canvas.Zoom;int baseWidth=(int)Math.Round(original.Width*scale),baseHeight=(int)Math.Round(original.Height*scale);
     // Calculate scroll bars from the viewport bounds, not its transient client size.
     int availableWidth=viewport.Width,availableHeight=viewport.Height;
     bool vertical=baseHeight>availableHeight,horizontal=baseWidth>availableWidth;
     for(int pass=0;pass<2;pass++){horizontal=baseWidth>availableWidth-(vertical?SystemInformation.VerticalScrollBarWidth:0);vertical=baseHeight>availableHeight-(horizontal?SystemInformation.HorizontalScrollBarHeight:0);}
     int width=Math.Max(baseWidth,availableWidth-(vertical?SystemInformation.VerticalScrollBarWidth:0)),height=Math.Max(baseHeight,availableHeight-(horizontal?SystemInformation.HorizontalScrollBarHeight:0));
     int dx=width-baseWidth,dy=height-baseHeight;viewport.SuspendLayout();canvas.SuspendLayout();canvas.Size=new Size(width,height);
     foreach(var item in layouts){if(item.Control.Parent!=canvas)continue;var r=item.Bounds;
      int x=(int)Math.Round(r.X*scale),y=(int)Math.Round(r.Y*scale),w=(int)Math.Round(r.Width*scale),h=(int)Math.Round(r.Height*scale);
      if(r.X>=780)x+=dx;
      else if(r.Width>=600||item.Control is MenuStrip||item.Control is BrightnessSlider)w+=dx;
      if(item.Control is Map)h+=dy;
      else if(r.Y>=408)y+=dy;
      var bounds=new Rectangle(x,y,w,h);if(item.Control.Bounds!=bounds)item.Control.Bounds=bounds;
     }
     viewport.AutoScrollMinSize=new Size(baseWidth,baseHeight);
    }finally{canvas.ResumeLayout(false);viewport.ResumeLayout(true);arranging=false;}
   };
   // Merge resize bursts into one update instead of queuing work for every event.
   var resizeTimer=new System.Windows.Forms.Timer{Interval=32};
   resizeTimer.Tick+=delegate{resizeTimer.Stop();fit();};
   viewport.SizeChanged+=delegate{if(responsive&&!arranging&&!form.IsDisposed)resizeTimer.Start();};
   form.Disposed+=delegate{resizeTimer.Dispose();};
   Button decrease=null,increase=null;Label zoomStatus=null,zoomValue=null;int barHeight=toolbar?48:0,currentPercent=Percent;
   if(toolbar){var bar=new Panel{Height=48,Dock=DockStyle.Top,BackColor=Color.White};form.Controls.Add(bar);viewport.BringToFront();
    bar.Controls.Add(new Label{Text="界面缩放",Bounds=new Rectangle(18,10,90,28),Font=Art.Font(16),ForeColor=Art.Ink});
    decrease=new SoftButton{Name="ZoomOut",Text="−",Bounds=new Rectangle(112,7,40,34),Font=Art.Font(20)};increase=new SoftButton{Name="ZoomIn",Text="+",Bounds=new Rectangle(232,7,40,34),Font=Art.Font(20)};bar.Controls.Add(decrease);bar.Controls.Add(increase);
    zoomValue=new Label{Name="ZoomValue",Bounds=new Rectangle(154,9,76,30),TextAlign=ContentAlignment.MiddleCenter,Font=Art.Font(16,true),ForeColor=Art.Ink};bar.Controls.Add(zoomValue);
    zoomStatus=new Label{Bounds=new Rectangle(290,10,750,28),Font=Art.Font(14),ForeColor=Art.Muted,Text="小窗口可滚动查看 · 支持最大化"};bar.Controls.Add(zoomStatus);
   }
   Action<int,bool> apply=delegate(int percent,bool resize){currentPercent=percent;if(zoomValue!=null){zoomValue.Text=percent+"%";decrease.Enabled=percent>100;increase.Enabled=percent<250;}float factor=percent/100f;canvas.SuspendLayout();viewport.SuspendLayout();viewport.AutoScrollPosition=Point.Empty;canvas.Zoom=factor;
    foreach(var item in layouts){var c=item.Control;c.Bounds=new Rectangle((int)Math.Round(item.Bounds.X*factor),(int)Math.Round(item.Bounds.Y*factor),(int)Math.Round(item.Bounds.Width*factor),(int)Math.Round(item.Bounds.Height*factor));var old=item.OwnedFont;item.OwnedFont=new Font(item.Font.FontFamily,item.Font.Size*factor,item.Font.Style,item.Font.Unit);c.Font=item.OwnedFont;if(old!=null)old.Dispose();var list=c as ListBox;if(list!=null){list.ItemHeight=(int)Math.Round(item.ItemHeight*factor);if(list.MultiColumn)list.ColumnWidth=(int)Math.Round(360*factor);}c.Invalidate();}
    canvas.Size=new Size((int)Math.Round(original.Width*factor),(int)Math.Round(original.Height*factor));viewport.AutoScrollMinSize=canvas.Size;canvas.ResumeLayout(false);viewport.ResumeLayout(true);
    if(resize&&form.WindowState!=FormWindowState.Maximized){var area=Screen.FromControl(form).WorkingArea;form.ClientSize=new Size(Math.Min(canvas.Width+22,Math.Max(540,area.Width-70)),Math.Min(canvas.Height+barHeight+22,Math.Max(420,area.Height-100)));form.Location=new Point(Math.Max(area.Left,Math.Min(form.Left,area.Right-form.Width)),Math.Max(area.Top,Math.Min(form.Top,area.Bottom-form.Height)));}
    if(savePreference){Percent=percent;try{Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath));File.WriteAllText(PreferencePath,percent.ToString());if(zoomStatus!=null)zoomStatus.Text="放大文字和控件 · 小窗口可滚动查看 · 支持最大化";}catch(IOException){if(zoomStatus!=null)zoomStatus.Text="缩放已生效，但未能保存设置。";}catch(UnauthorizedAccessException){if(zoomStatus!=null)zoomStatus.Text="缩放已生效，但未能保存设置。";}}
   };
   if(increase!=null){increase.Click+=delegate{apply(Math.Min(250,currentPercent+25),true);};decrease.Click+=delegate{apply(Math.Max(100,currentPercent-25),true);};}
   apply(Percent,true);fit();
   form.FormClosed+=delegate{foreach(var item in layouts)if(item.OwnedFont!=null)item.OwnedFont.Dispose();};
  }
 }
}

