// IconGen.cs — build-time tool (not part of the shipped app).
//
// Draws the application icon at every size Windows asks for and writes a
// multi-image .ico: DIB entries for 16..128 (maximum shell compatibility) and a
// PNG entry for 256 (the Vista+ convention for the large size).
//
//   csc /out:tools\IconGen.exe tools\IconGen.cs
//   tools\IconGen.exe src\app.ico

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

internal static class IconGen
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };

    private static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "app.ico";

        List<byte[]> images = new List<byte[]>();
        List<int> sizes = new List<int>();

        foreach (int size in Sizes)
        {
            using (Bitmap bitmap = Render(size))
            {
                bool asPng = size >= 256;
                images.Add(asPng ? EncodePng(bitmap) : EncodeDib(bitmap));
                sizes.Add(size);
                Console.WriteLine("  {0,3}x{0,-3} {1}", size, asPng ? "PNG" : "DIB");
            }
        }

        string directory = Path.GetDirectoryName(Path.GetFullPath(output));
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using (FileStream stream = File.Create(output))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);              // reserved
            writer.Write((ushort)1);              // type: icon
            writer.Write((ushort)images.Count);   // image count

            int offset = 6 + images.Count * 16;
            for (int i = 0; i < images.Count; i++)
            {
                int size = sizes[i];
                writer.Write((byte)(size >= 256 ? 0 : size)); // 0 means 256
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);            // palette count
                writer.Write((byte)0);            // reserved
                writer.Write((ushort)1);          // colour planes
                writer.Write((ushort)32);         // bits per pixel
                writer.Write(images[i].Length);
                writer.Write(offset);
                offset += images[i].Length;
            }

            foreach (byte[] image in images) writer.Write(image);
        }

        // Fail the build loudly rather than shipping an icon the shell can't read.
        using (Icon check = new Icon(output, 32, 32))
        {
            Console.WriteLine("已生成 {0}（{1} 个尺寸，最高 {2}x{2}，验证尺寸 {3}x{3}）",
                output, images.Count, Sizes[Sizes.Length - 1], check.Width);
        }
        return 0;
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private static Bitmap Render(int size)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Rounded-square body with a blue gradient.
            float radius = size * 0.22F;
            RectangleF body = new RectangleF(0.5F, 0.5F, size - 1F, size - 1F);
            using (GraphicsPath path = RoundedRect(body, radius))
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new RectangleF(0, 0, size, size),
                Color.FromArgb(0x4F, 0x8D, 0xF7),
                Color.FromArgb(0x1D, 0x4E, 0xD8),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillPath(brush, path);
            }

            bool simplified = size <= 20; // strokes blur together below 20px
            float shift = 0.009F * size;  // optically centre the glyph
            PointF[] speaker = new PointF[]
            {
                new PointF(shift + size * 0.255F, size * 0.405F),
                new PointF(shift + size * 0.375F, size * 0.405F),
                new PointF(shift + size * 0.515F, size * 0.275F),
                new PointF(shift + size * 0.515F, size * 0.725F),
                new PointF(shift + size * 0.375F, size * 0.595F),
                new PointF(shift + size * 0.255F, size * 0.595F)
            };

            using (SolidBrush white = new SolidBrush(Color.White))
                g.FillPolygon(white, speaker);

            float centreX = shift + size * 0.515F;
            float centreY = size * 0.5F;
            float penWidth = size * (simplified ? 0.095F : 0.055F);

            using (Pen pen = new Pen(Color.White, penWidth))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                float innerRadius = size * (simplified ? 0.145F : 0.105F);
                float innerDiameter = innerRadius * 2F;
                g.DrawArc(pen,
                    centreX - innerRadius, centreY - innerRadius, innerDiameter, innerDiameter,
                    -55F, 110F);

                if (!simplified)
                {
                    float outerRadius = size * 0.185F;
                    float outerDiameter = outerRadius * 2F;
                    g.DrawArc(pen,
                        centreX - outerRadius, centreY - outerRadius, outerDiameter, outerDiameter,
                        -55F, 110F);
                }
            }
        }
        return bitmap;
    }

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float d = radius * 2F;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ------------------------------------------------------------------
    // ICO encoding
    // ------------------------------------------------------------------

    private static byte[] EncodePng(Bitmap bitmap)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }

    private static byte[] EncodeDib(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        byte[] pixels = new byte[data.Stride * height];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        bitmap.UnlockBits(data);

        int maskStride = ((width + 31) / 32) * 4;
        byte[] mask = new byte[maskStride * height];

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            // BITMAPINFOHEADER — height is doubled to cover XOR + AND masks.
            writer.Write(40);
            writer.Write(width);
            writer.Write(height * 2);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(0);                  // BI_RGB
            writer.Write(width * height * 4);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            // XOR bitmap, bottom-up BGRA.
            for (int y = height - 1; y >= 0; y--)
                writer.Write(pixels, y * data.Stride, width * 4);

            // AND mask, bottom-up. Bit set = transparent, which keeps the icon
            // correct on the legacy code paths that ignore the alpha channel.
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    Color colour = bitmap.GetPixel(x, y);
                    if (colour.A < 128)
                        mask[y * maskStride + (x / 8)] |= (byte)(0x80 >> (x % 8));
                }
            }
            writer.Write(mask);

            return stream.ToArray();
        }
    }
}
