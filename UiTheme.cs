using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace JiePing
{
    internal static class UiTheme
    {
        public static readonly Color Background = Color.FromArgb(11, 12, 16);
        public static readonly Color Panel = Color.FromArgb(16, 18, 23);
        public static readonly Color Card = Color.FromArgb(19, 21, 27);
        public static readonly Color CardImage = Color.FromArgb(13, 15, 19);
        public static readonly Color Border = Color.FromArgb(37, 40, 49);
        public static readonly Color BorderHover = Color.FromArgb(88, 93, 105);
        public static readonly Color Text = Color.FromArgb(245, 243, 237);
        public static readonly Color Muted = Color.FromArgb(141, 147, 159);
        public static readonly Color Faint = Color.FromArgb(92, 98, 110);
        public static readonly Color Accent = Color.FromArgb(255, 104, 72);
        public static readonly Color AccentSoft = Color.FromArgb(255, 137, 112);

        public static Font UiFont(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        }

        public static ContextMenuStrip CreateMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(22, 24, 30);
            menu.ForeColor = Text;
            menu.Font = UiFont(9.5f, FontStyle.Regular);
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(5, 6, 5, 6);
            menu.Renderer = new DarkMenuRenderer();
            return menu;
        }

        public static ToolStripMenuItem MenuItem(string text)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = Text;
            item.BackColor = Color.Transparent;
            item.Padding = new Padding(8, 0, 12, 0);
            return item;
        }

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (r.Width < d || r.Height < d)
            {
                d = Math.Min(r.Width, r.Height);
            }
            if (d <= 0f)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(22, 24, 30); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(22, 24, 30); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(22, 24, 30); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(22, 24, 30); } }
        public override Color MenuBorder { get { return Color.FromArgb(46, 49, 58); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(70, 74, 85); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(38, 41, 50); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(38, 41, 50); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(38, 41, 50); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(32, 35, 43); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(32, 35, 43); } }
        public override Color SeparatorDark { get { return Color.FromArgb(46, 49, 58); } }
        public override Color SeparatorLight { get { return Color.FromArgb(46, 49, 58); } }
        public override Color CheckBackground { get { return Color.FromArgb(22, 24, 30); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(38, 41, 50); } }
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? UiTheme.Text : UiTheme.Faint;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (Pen pen = new Pen(Color.FromArgb(48, 51, 61)))
            {
                e.Graphics.DrawRectangle(pen, r);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen pen = new Pen(Color.FromArgb(42, 45, 54)))
            {
                e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = new Rectangle(e.ImageRectangle.Left + 2, e.ImageRectangle.Top + 3, 13, 13);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiTheme.RoundedRect(r, 3f))
            using (SolidBrush fill = new SolidBrush(UiTheme.Accent))
            {
                e.Graphics.FillPath(fill, path);
            }
            using (Pen pen = new Pen(Color.White, 2f))
            {
                e.Graphics.DrawLines(pen, new Point[]
                {
                    new Point(r.Left + 3, r.Top + 7),
                    new Point(r.Left + 6, r.Top + 10),
                    new Point(r.Left + 11, r.Top + 4)
                });
            }
        }
    }
}
