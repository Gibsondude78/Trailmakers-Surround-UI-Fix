using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using TrailmakersSurroundUI;

internal static class MakeIcon
{
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Supply the assets directory.");
        Directory.CreateDirectory(args[0]);
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        List<byte[]> images = new List<byte[]>();
        foreach (int size in sizes)
        {
            using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (MemoryStream buffer = new MemoryStream())
            {
                graphics.Clear(Color.Transparent);
                Branding.DrawMark(graphics, new RectangleF(0, 0, size, size));
                bitmap.Save(buffer, ImageFormat.Png);
                images.Add(buffer.ToArray());
                if (size == 256) bitmap.Save(Path.Combine(args[0], "setup-icon.png"), ImageFormat.Png);
            }
        }
        using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(args[0], "setup.ico"))))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; ++i)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] bytes in images) writer.Write(bytes);
        }
        Console.WriteLine("Created setup.ico and setup-icon.png.");
    }
}
