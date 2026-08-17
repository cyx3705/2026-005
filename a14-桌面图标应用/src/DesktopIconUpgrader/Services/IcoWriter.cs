using System.Buffers.Binary;
using System.Windows.Media.Imaging;

namespace DesktopIconUpgrader.Services;

public sealed class IcoWriter
{
    public static readonly int[] RequiredSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    public void Write(string path, BitmapSource master)
    {
        var frames = RequiredSizes.Select(size => (Size: size, Data: EncodePng(IconRenderer.Scale(master, size, size)))).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        using (var stream = File.Create(tempPath))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)frames.Count);
            var offset = 6 + frames.Count * 16;
            foreach (var frame in frames)
            {
                writer.Write((byte)(frame.Size == 256 ? 0 : frame.Size));
                writer.Write((byte)(frame.Size == 256 ? 0 : frame.Size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write(frame.Data.Length);
                writer.Write(offset);
                offset += frame.Data.Length;
            }
            foreach (var frame in frames) writer.Write(frame.Data);
        }
        Validate(tempPath);
        File.Move(tempPath, path, true);
    }

    public static void Validate(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)) != 1)
            throw new InvalidDataException("ICO 文件头无效");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        var found = new HashSet<int>();
        for (var index = 0; index < count; index++)
        {
            var entry = 6 + index * 16;
            if (entry + 16 > bytes.Length) throw new InvalidDataException("ICO 目录不完整");
            var size = bytes[entry] == 0 ? 256 : bytes[entry];
            var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(entry + 8, 4));
            var offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(entry + 12, 4));
            if (length <= 0 || offset < 0 || offset + length > bytes.Length)
                throw new InvalidDataException($"ICO {size}px 帧无效");
            found.Add(size);
        }
        var missing = RequiredSizes.Where(size => !found.Contains(size)).ToArray();
        if (missing.Length > 0) throw new InvalidDataException($"ICO 缺少尺寸：{string.Join(", ", missing)}");
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
