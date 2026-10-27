using System.Drawing;
using System.Drawing.Drawing2D;

namespace TrailmakersSurroundUI
{
    internal static class Branding
    {
        public static readonly Color Yellow = Color.FromArgb(255, 200, 36);
        public static readonly Color Background = Color.FromArgb(31, 37, 44);
        public static readonly Color Panel = Color.FromArgb(40, 48, 57);
        public static readonly Color Muted = Color.FromArgb(167, 178, 190);
        public static readonly Color White = Color.FromArgb(240, 243, 246);

        // The same vector mark is used in the header and all Windows icon sizes.
        public static void DrawMark(Graphics graphics, RectangleF bounds)
        {
            GraphicsState state = graphics.Save();
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 256f, bounds.Height / 256f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(20, 26, 33)))
            using (SolidBrush edge = new SolidBrush(Color.FromArgb(89, 104, 119)))
            using (SolidBrush yellow = new SolidBrush(Yellow))
            using (SolidBrush glass = new SolidBrush(Color.FromArgb(38, 49, 61)))
            using (SolidBrush white = new SolidBrush(Color.FromArgb(211, 220, 228)))
            {
                graphics.FillPolygon(dark, new[] { new PointF(32, 12), new PointF(224, 12),
                    new PointF(244, 32), new PointF(244, 224), new PointF(224, 244),
                    new PointF(32, 244), new PointF(12, 224), new PointF(12, 32) });
                graphics.FillPolygon(yellow, new[] { new PointF(32, 12), new PointF(224, 12),
                    new PointF(236, 24), new PointF(37, 24), new PointF(24, 37), new PointF(24, 72),
                    new PointF(12, 72), new PointF(12, 32) });
                graphics.FillPolygon(edge, new[] { new PointF(28, 80), new PointF(80, 91),
                    new PointF(80, 177), new PointF(28, 166) });
                graphics.FillPolygon(glass, new[] { new PointF(35, 90), new PointF(73, 98),
                    new PointF(73, 167), new PointF(35, 159) });
                graphics.FillPolygon(edge, new[] { new PointF(176, 91), new PointF(228, 80),
                    new PointF(228, 166), new PointF(176, 177) });
                graphics.FillPolygon(glass, new[] { new PointF(183, 98), new PointF(221, 90),
                    new PointF(221, 159), new PointF(183, 167) });
                graphics.FillRectangle(yellow, 88, 72, 80, 105);
                graphics.FillRectangle(dark, 104, 111, 48, 35);
                graphics.FillRectangle(dark, 109, 101, 13, 13);
                graphics.FillRectangle(dark, 134, 101, 13, 13);
                graphics.FillRectangle(yellow, 111, 119, 9, 9);
                graphics.FillRectangle(yellow, 136, 119, 9, 9);
                graphics.FillRectangle(white, 118, 184, 20, 18);
                graphics.FillPolygon(white, new[] { new PointF(95, 204), new PointF(161, 204),
                    new PointF(169, 215), new PointF(87, 215) });
            }
            graphics.Restore(state);
        }
    }
}
