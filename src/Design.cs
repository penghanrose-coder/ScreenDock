using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ScreenDock {
 public class ModeCombo : ComboBox {
  public ModeCombo(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);DropDownStyle=ComboBoxStyle.DropDownList;}
  protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ItemHeight=Math.Max(24,Font.Height+8);}
  protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
  protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Enabled?Color.White:Color.FromArgb(248,249,251));using(var pen=new Pen(Color.FromArgb(190,198,208)))g.DrawRectangle(pen,0,0,Width-1,Height-1);var option=SelectedItem as ModeOption;string text=option==null?"—":option.Label;TextRenderer.DrawText(g,text,Font,new Rectangle(10,1,Width-40,Height-2),Enabled?Art.Ink:Art.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);using(var pen=new Pen(Art.Muted,1.4f)){int x=Width-19,y=Height/2;g.DrawLines(pen,new[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});}if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-30,Height-6));}
 }
 public static class Art {
  public static Color Background=Color.FromArgb(245,246,250),Panel=Color.White,Accent=Color.FromArgb(0,103,210),Muted=Color.FromArgb(100,109,121),Ink=Color.FromArgb(28,32,37),Line=Color.FromArgb(233,235,242);
  public static Font Font(float size,bool bold=false){return new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);}
  public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  public static void Fill(Graphics g,RectangleF r,float radius,Color color){using(var p=Round(r,radius))using(var b=new SolidBrush(color))g.FillPath(b,p);}
  public static void Text(Graphics g,string text,Font font,Color color,Rectangle bounds,TextFormatFlags flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis){TextRenderer.DrawText(g,text,font,bounds,color,flags|TextFormatFlags.NoPadding);}
  public static Bitmap Logo(int size){
   var bitmap=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
   using(var g=Graphics.FromImage(bitmap)){
    g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/64f,size/64f);
    Fill(g,new RectangleF(2,2,60,60),13,Color.FromArgb(13,78,157));
    Fill(g,new RectangleF(25,14,29,23),3,Color.FromArgb(99,213,243));
    Fill(g,new RectangleF(28,17,23,17),1,Color.FromArgb(25,116,191));
    Fill(g,new RectangleF(10,26,34,25),3,Color.FromArgb(248,252,255));
    Fill(g,new RectangleF(13,29,28,19),1,Color.FromArgb(25,116,191));
   }return bitmap;
  }
  public static byte[] IconData(){
   var frames=new List<byte[]>();int[] sizes={16,20,24,32,40,48,64,128,256};
   foreach(int size in sizes)using(var bitmap=Logo(size))using(var stream=new System.IO.MemoryStream()){bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);frames.Add(stream.ToArray());}
   using(var stream=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(stream)){
    writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
    for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;}
    foreach(var frame in frames)writer.Write(frame);return stream.ToArray();
   }
  }
  public static Icon Icon(){using(var stream=new System.IO.MemoryStream(IconData()))using(var icon=new Icon(stream,new Size(32,32)))return (Icon)icon.Clone();}
 }
 public class Card : Panel {
  public Card(){DoubleBuffered=true;BackColor=Art.Background;}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Art.Fill(e.Graphics,new RectangleF(0,0,Width-1,Height-1),18,Color.White);using(var p=Art.Round(new RectangleF(.5f,.5f,Width-2,Height-2),18))using(var pen=new Pen(Art.Line))e.Graphics.DrawPath(pen,p);}
 }
 public class SoftButton : Button {
  bool hover,down;public bool Primary;
  public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=Art.Font(13);}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;down=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?Art.Background:Parent is Card?Color.White:Parent.BackColor);var fill=!Enabled?Color.FromArgb(243,244,248):Primary?(down?Color.FromArgb(0,80,165):hover?Color.FromArgb(20,118,220):Art.Accent):(hover?Color.FromArgb(232,241,252):Color.FromArgb(248,249,251));Art.Fill(g,new RectangleF(0,0,Width-1,Height-1),3,fill);using(var outline=Art.Round(new RectangleF(.5f,.5f,Width-2,Height-2),3))using(var border=new Pen(Color.FromArgb(193,201,212)))g.DrawPath(border,outline);Art.Text(g,Text,Font,!Enabled?Color.FromArgb(167,172,186):Primary?Color.White:Art.Ink,ClientRectangle,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-9,Height-9));}
 }
 public class BrightnessSlider : Control {
  int value=50;bool dragging;public event EventHandler ValueChanged,Committed;
  public int Value{get{return value;}set{this.value=Math.Max(0,Math.Min(100,value));Invalidate();}}
  public BrightnessSlider(){DoubleBuffered=true;TabStop=true;Cursor=Cursors.Hand;BackColor=Color.White;AccessibleName="显示器亮度";AccessibleRole=AccessibleRole.Slider;}
  void UpdateValue(int next){Value=next;if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}
  void FromMouse(int x){UpdateValue((int)Math.Round((x-10)*100.0/Math.Max(1,Width-20)));}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;int y=Height/2;float x=10+(Width-20)*value/100f;Art.Fill(g,new RectangleF(8,y-3,Width-16,6),3,Art.Line);if(x>10)Art.Fill(g,new RectangleF(8,y-3,x-8,6),3,Enabled?Art.Accent:Color.FromArgb(204,207,218));using(var b=new SolidBrush(Enabled?Art.Accent:Color.FromArgb(204,207,218)))g.FillEllipse(b,x-8,y-8,16,16);using(var b=new SolidBrush(Color.White))g.FillEllipse(b,x-3,y-3,6,6);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(1,1,Width-3,Height-3));}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();dragging=true;Capture=true;FromMouse(e.X);}}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)FromMouse(e.X);}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(dragging&&e.Button==MouseButtons.Left){FromMouse(e.X);dragging=false;Capture=false;if(Committed!=null)Committed(this,EventArgs.Empty);}}
  protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)dragging=false;base.OnMouseCaptureChanged(e);}
  protected override bool IsInputKey(Keys keyData){Keys k=keyData&Keys.KeyCode;return k==Keys.Left||k==Keys.Right||k==Keys.Home||k==Keys.End||k==Keys.PageUp||k==Keys.PageDown||base.IsInputKey(keyData);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);switch(e.KeyCode){case Keys.Left:UpdateValue(value-1);break;case Keys.Right:UpdateValue(value+1);break;case Keys.Home:UpdateValue(0);break;case Keys.End:UpdateValue(100);break;case Keys.PageUp:UpdateValue(value+10);break;case Keys.PageDown:UpdateValue(value-10);break;default:return;}e.Handled=true;}
  protected override void OnKeyUp(KeyEventArgs e){base.OnKeyUp(e);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right||e.KeyCode==Keys.Home||e.KeyCode==Keys.End||e.KeyCode==Keys.PageUp||e.KeyCode==Keys.PageDown)if(Committed!=null)Committed(this,EventArgs.Empty);}
 }
 public partial class Map : Control {
  public List<Display> Displays=new List<Display>();public string Selected;public Action<Display> Pick;Dictionary<Rectangle,Display> hit=new Dictionary<Rectangle,Display>();
  public Map(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Color.FromArgb(240,243,247);Cursor=Cursors.Hand;}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;hit.Clear();
   var active=Displays.Where(d=>d.Active&&d.Mode.width>0).ToList();if(active.Count==0){Art.Text(g,"暂无活动屏幕",Art.Font(14),Art.Muted,ClientRectangle,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}
   int left=active.Min(d=>d.Mode.x),top=active.Min(d=>d.Mode.y);double w=active.Max(d=>d.Mode.x+(double)d.Mode.width)-left,h=active.Max(d=>d.Mode.y+(double)d.Mode.height)-top;float scale=(float)Math.Min((Width-95)/Math.Max(w,1),(Height-90)/Math.Max(h,1));
   float ox=(float)((Width-w*scale)/2-left*scale),oy=(float)((Height-h*scale)/2-top*scale)-10;if(dragTarget!=null){scale=dragScale;ox=dragOrigin.X;oy=dragOrigin.Y;}else{lastScale=scale;lastOrigin=new PointF(ox,oy);}
   foreach(var d in active){bool chosen=d.Device==Selected;var r=new Rectangle((int)(ox+d.Mode.x*scale),(int)(oy+d.Mode.y*scale),Math.Max(16,(int)(d.Mode.width*scale)-10),Math.Max(16,(int)(d.Mode.height*scale)-8));
    using(var path=Art.Round(r,1))using(var brush=new LinearGradientBrush(r,chosen?Color.FromArgb(57,132,228):Color.FromArgb(96,103,111),chosen?Color.FromArgb(39,109,201):Color.FromArgb(75,82,90),45)){g.FillPath(brush,path);using(var pen=new Pen(chosen?Art.Accent:Color.FromArgb(169,182,210),2))g.DrawPath(pen,path);}
    using(var f=Art.Font(32*UiZoom.Factor(this),true))Art.Text(g,d.Number.ToString(),f,Color.White,r,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    using(var f=Art.Font(11*UiZoom.Factor(this)))Art.Text(g,d.Mode.width+" × "+d.Mode.height+"  "+d.Mode.frequency+" Hz",f,Art.Muted,new Rectangle(r.X,r.Bottom+22,r.Width,(int)(22*UiZoom.Factor(this))),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);hit[r]=d;
   }
  }
  protected override void OnMouseClick(MouseEventArgs e){if(DragEnabled)return;foreach(var p in hit)if(p.Key.Contains(e.Location)&&Pick!=null){Pick(p.Value);break;}}
 }
}




