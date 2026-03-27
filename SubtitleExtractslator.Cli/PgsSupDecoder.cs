using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SubtitleExtractslator.Cli;

internal sealed record PgsDecodedFrame(string ImagePath, TimeSpan Start, TimeSpan End);

internal static class PgsSupDecoder
{
    private const uint PtsClock = 90_000;

    public static List<PgsDecodedFrame> DecodeToPngFrames(string supPath, string outputDirectory)
    {
        using var fs = File.OpenRead(supPath);
        using var br = new BinaryReader(fs);

        var palettes = new Dictionary<byte, Rgba32[]>(256);
        var objects = new Dictionary<ushort, ObjectDefinition>();
        var assemblies = new Dictionary<ushort, ObjectAssembly>();

        DisplaySet? current = null;
        var starts = new List<(string ImagePath, TimeSpan Start)>();

        while (br.BaseStream.Position + 13 <= br.BaseStream.Length)
        {
            var magic1 = br.ReadByte();
            var magic2 = br.ReadByte();
            if (magic1 != 0x50 || magic2 != 0x47)
            {
                throw new InvalidOperationException("Invalid SUP stream: missing PG segment header.");
            }

            var pts = ReadUInt32BE(br);
            _ = ReadUInt32BE(br); // dts
            var segmentType = br.ReadByte();
            var segmentLength = ReadUInt16BE(br);
            var payload = br.ReadBytes(segmentLength);
            if (payload.Length != segmentLength)
            {
                break;
            }

            switch (segmentType)
            {
                case 0x14: // PDS
                    ParsePalette(payload, palettes);
                    break;
                case 0x15: // ODS
                    ParseObject(payload, assemblies, objects);
                    break;
                case 0x16: // PCS
                    current = ParseComposition(payload, pts);
                    break;
                case 0x17: // WDS
                    break;
                case 0x80: // END
                    if (current is not null)
                    {
                        var rendered = RenderDisplaySet(current, palettes, objects, outputDirectory, starts.Count + 1);
                        if (!string.IsNullOrWhiteSpace(rendered))
                        {
                            starts.Add((rendered, current.Start));
                        }

                        current = null;
                    }
                    break;
            }
        }

        if (starts.Count == 0)
        {
            return new List<PgsDecodedFrame>();
        }

        var output = new List<PgsDecodedFrame>(starts.Count);
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i].Start;
            var end = i + 1 < starts.Count
                ? ResolveNextEnd(start, starts[i + 1].Start)
                : start + TimeSpan.FromSeconds(2);

            output.Add(new PgsDecodedFrame(starts[i].ImagePath, start, end));
        }

        return output;
    }

    private static TimeSpan ResolveNextEnd(TimeSpan start, TimeSpan nextStart)
    {
        if (nextStart <= start)
        {
            return start + TimeSpan.FromMilliseconds(400);
        }

        var gap = nextStart - start;
        if (gap < TimeSpan.FromMilliseconds(300))
        {
            return start + TimeSpan.FromMilliseconds(300);
        }

        return nextStart;
    }

    private static string? RenderDisplaySet(
        DisplaySet set,
        Dictionary<byte, Rgba32[]> palettes,
        Dictionary<ushort, ObjectDefinition> objects,
        string outputDirectory,
        int index)
    {
        if (!palettes.TryGetValue(set.PaletteId, out var palette))
        {
            return null;
        }

        using var image = new Image<Rgba32>(Math.Max(1, set.Width), Math.Max(1, set.Height));
        var anyDrawn = false;

        foreach (var placement in set.Objects)
        {
            if (!objects.TryGetValue(placement.ObjectId, out var obj))
            {
                continue;
            }

            var pixels = DecodeRle(obj.PixelData, obj.Width, obj.Height);
            if (pixels.Length == 0)
            {
                continue;
            }

            for (var y = 0; y < obj.Height; y++)
            {
                var targetY = placement.Y + y;
                if (targetY < 0 || targetY >= image.Height)
                {
                    continue;
                }

                for (var x = 0; x < obj.Width; x++)
                {
                    var targetX = placement.X + x;
                    if (targetX < 0 || targetX >= image.Width)
                    {
                        continue;
                    }

                    var idx = pixels[(y * obj.Width) + x];
                    var color = palette[idx];
                    if (color.A == 0)
                    {
                        continue;
                    }

                    image[targetX, targetY] = color;
                    anyDrawn = true;
                }
            }
        }

        if (!anyDrawn)
        {
            return null;
        }

        var outputPath = Path.Combine(outputDirectory, $"cue_{index:D6}.png");
        image.SaveAsPng(outputPath);
        return outputPath;
    }

    private static byte[] DecodeRle(byte[] data, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return Array.Empty<byte>();
        }

        var output = new byte[width * height];
        var x = 0;
        var y = 0;
        var p = 0;

        while (p < data.Length && y < height)
        {
            var b = data[p++];
            if (b != 0)
            {
                SetPixel(output, width, height, ref x, ref y, b);
                continue;
            }

            if (p >= data.Length)
            {
                break;
            }

            var b2 = data[p++];
            if (b2 == 0)
            {
                x = 0;
                y++;
                continue;
            }

            var isLong = (b2 & 0x40) != 0;
            var hasColor = (b2 & 0x80) != 0;
            var run = b2 & 0x3F;
            if (isLong)
            {
                if (p >= data.Length)
                {
                    break;
                }

                run = (run << 8) | data[p++];
            }

            byte color = 0;
            if (hasColor)
            {
                if (p >= data.Length)
                {
                    break;
                }

                color = data[p++];
            }

            if (run <= 0)
            {
                continue;
            }

            for (var i = 0; i < run; i++)
            {
                SetPixel(output, width, height, ref x, ref y, color);
                if (y >= height)
                {
                    break;
                }
            }
        }

        return output;
    }

    private static void SetPixel(byte[] output, int width, int height, ref int x, ref int y, byte value)
    {
        if (y >= height)
        {
            return;
        }

        if (x >= width)
        {
            x = 0;
            y++;
            if (y >= height)
            {
                return;
            }
        }

        output[(y * width) + x] = value;
        x++;
    }

    private static void ParsePalette(byte[] payload, Dictionary<byte, Rgba32[]> palettes)
    {
        if (payload.Length < 2)
        {
            return;
        }

        var paletteId = payload[0];
        var table = new Rgba32[256];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = new Rgba32(0, 0, 0, 0);
        }

        var p = 2;
        while (p + 4 < payload.Length)
        {
            var entry = payload[p++];
            var y = payload[p++];
            var cr = payload[p++];
            var cb = payload[p++];
            var alpha = payload[p++];
            table[entry] = YCrCbToRgba(y, cr, cb, alpha);
        }

        palettes[paletteId] = table;
    }

    private static void ParseObject(
        byte[] payload,
        Dictionary<ushort, ObjectAssembly> assemblies,
        Dictionary<ushort, ObjectDefinition> objects)
    {
        if (payload.Length < 7)
        {
            return;
        }

        var objectId = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2));
        var sequence = payload[3];
        var isFirst = (sequence & 0x80) != 0;
        var isLast = (sequence & 0x40) != 0;

        var p = 4;
        ObjectAssembly assembly;
        if (isFirst)
        {
            if (payload.Length < 11)
            {
                return;
            }

            var totalLength = (payload[p] << 16) | (payload[p + 1] << 8) | payload[p + 2];
            p += 3;
            var width = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(p, 2));
            p += 2;
            var height = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(p, 2));
            p += 2;

            assembly = new ObjectAssembly(width, height, Math.Max(0, totalLength - 4));
            assemblies[objectId] = assembly;
        }
        else
        {
            if (!assemblies.TryGetValue(objectId, out assembly!))
            {
                return;
            }
        }

        if (p < payload.Length)
        {
            assembly.Append(payload.AsSpan(p));
        }

        if (isLast)
        {
            objects[objectId] = new ObjectDefinition(assembly.Width, assembly.Height, assembly.ToArray());
            assemblies.Remove(objectId);
        }
    }

    private static DisplaySet? ParseComposition(byte[] payload, uint pts)
    {
        if (payload.Length < 11)
        {
            return null;
        }

        var p = 0;
        var width = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(p, 2));
        p += 2;
        var height = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(p, 2));
        p += 2;
        p++; // frame rate
        p += 2; // composition number
        p++; // composition state
        p++; // palette update flag
        var paletteId = payload[p++];
        var objectCount = payload[p++];

        var list = new List<ObjectPlacement>(objectCount);
        for (var i = 0; i < objectCount; i++)
        {
            if (p + 7 > payload.Length)
            {
                break;
            }

            var objectId = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(p, 2));
            p += 2;
            p++; // window id
            var compositionFlag = payload[p++];
            var x = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(p, 2));
            p += 2;
            var y = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(p, 2));
            p += 2;

            if ((compositionFlag & 0x40) != 0 && p + 8 <= payload.Length)
            {
                p += 8; // crop info
            }

            list.Add(new ObjectPlacement(objectId, x, y));
        }

        var start = TimeSpan.FromSeconds(pts / (double)PtsClock);
        return new DisplaySet(width, height, paletteId, start, list);
    }

    private static Rgba32 YCrCbToRgba(byte y, byte cr, byte cb, byte alpha)
    {
        var yf = y;
        var crf = cr - 128f;
        var cbf = cb - 128f;
        var r = ClampToByte(yf + (1.402f * crf));
        var g = ClampToByte(yf - (0.344136f * cbf) - (0.714136f * crf));
        var b = ClampToByte(yf + (1.772f * cbf));
        return new Rgba32(r, g, b, alpha);
    }

    private static byte ClampToByte(float value)
    {
        if (value <= 0)
        {
            return 0;
        }

        if (value >= 255)
        {
            return 255;
        }

        return (byte)value;
    }

    private static ushort ReadUInt16BE(BinaryReader br)
    {
        var bytes = br.ReadBytes(2);
        if (bytes.Length < 2)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private static uint ReadUInt32BE(BinaryReader br)
    {
        var bytes = br.ReadBytes(4);
        if (bytes.Length < 4)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private sealed class ObjectAssembly
    {
        private readonly MemoryStream _data;

        public ObjectAssembly(ushort width, ushort height, int expectedLength)
        {
            Width = width;
            Height = height;
            _data = expectedLength > 0 ? new MemoryStream(expectedLength) : new MemoryStream();
        }

        public ushort Width { get; }

        public ushort Height { get; }

        public void Append(ReadOnlySpan<byte> bytes)
        {
            _data.Write(bytes);
        }

        public byte[] ToArray() => _data.ToArray();
    }

    private sealed record ObjectDefinition(int Width, int Height, byte[] PixelData);

    private sealed record ObjectPlacement(ushort ObjectId, int X, int Y);

    private sealed record DisplaySet(int Width, int Height, byte PaletteId, TimeSpan Start, List<ObjectPlacement> Objects);
}
