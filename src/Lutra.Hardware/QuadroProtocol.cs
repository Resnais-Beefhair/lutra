using System.Buffers.Binary;

using Lutra.Core;
namespace Lutra.Hardware;

public record FanReading(double Output, int Rpm, double Voltage, double Current, double Power);
public record QuadroReading(DateTimeOffset Time, string Serial, int Firmware, double?[] Temperatures,
    double?[] SoftwareTemperatures, double? FlowLitresPerHour, FanReading[] Fans);
public record ChannelSettings(int Channel, byte Mode, int Source, double Minimum, double Maximum,
    double Fallback, bool HoldMinimum, bool StartBoost, double StartupTemperature, CurvePoint[] Points);
public record QuadroSettings(ushort Structure, int FlowPulsesPerLitre, double FlowCorrection,
    ChannelSettings[] Channels, byte Profile);

/// <summary>QUADRO USB report layout. See docs/PROTOCOL.md for provenance and validation limits.</summary>
public static class QuadroProtocol
{
    public const int SettingsLength = 0x3C1;
    public const int StatusLength = 0xDC;
    public static readonly int[] ControllerOffsets = [0x36, 0x8B, 0xE0, 0x135];
    private static readonly int[] FanOffsets = [0x70, 0x7D, 0x8A, 0x97];
    public static ushort U16(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
    public static short S16(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt16BigEndian(data[offset..]);
    public static void Put16(Span<byte> data, int offset, int value) => BinaryPrimitives.WriteUInt16BigEndian(data[offset..], checked((ushort)value));
    public static ushort Crc(ReadOnlySpan<byte> bytes)
    {
        ushort crc = 0xFFFF;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++) crc = (ushort)((crc >> 1) ^ ((crc & 1) != 0 ? 0xA001 : 0));
        }
        return (ushort)(crc ^ 0xFFFF);
    }
    public static bool ValidCrc(ReadOnlySpan<byte> bytes) => bytes.Length >= 3 && U16(bytes, bytes.Length - 2) == Crc(bytes[1..^2]);
    public static void Seal(Span<byte> bytes) => Put16(bytes, bytes.Length - 2, Crc(bytes[1..^2]));
    private static double? Temp(ReadOnlySpan<byte> bytes, int offset)
    {
        var raw = S16(bytes, offset);
        return raw == 0x7FFF ? null : raw / 100.0;
    }
    public static QuadroReading ReadStatus(ReadOnlySpan<byte> data, DateTimeOffset? now = null)
    {
        if (data.Length < StatusLength || data[0] != 1) throw new InvalidDataException("Unexpected QUADRO status report.");
        var temps = new double?[4]; var soft = new double?[16]; var fans = new FanReading[4];
        for (int i = 0; i < 4; i++)
        {
            temps[i] = Temp(data, 0x34 + i * 2);
            var b = FanOffsets[i];
            fans[i] = new(U16(data, b) / 100.0, U16(data, b + 8), U16(data, b + 2) / 100.0,
                U16(data, b + 4) / 1000.0, U16(data, b + 6) / 100.0);
        }
        for (int i = 0; i < soft.Length; i++) soft[i] = Temp(data, 0x3C + i * 2);
        var flow = U16(data, 0x6E);
        return new(now ?? DateTimeOffset.UtcNow, $"{U16(data, 3):D5}-{U16(data, 5):D5}", U16(data, 13),
            temps, soft, flow == 0x7FFF ? null : flow / 10.0, fans);
    }
    public static QuadroSettings ReadSettings(ReadOnlySpan<byte> data)
    {
        if (data.Length != SettingsLength || data[0] != 3 || !ValidCrc(data))
            throw new InvalidDataException("Invalid QUADRO settings report or checksum.");
        var channels = new ChannelSettings[4];
        for (int i = 0; i < 4; i++)
        {
            var b = ControllerOffsets[i]; var rec = 0x12 + 9 * i;
            var points = new CurvePoint[16];
            for (int k = 0; k < 16; k++) points[k] = new(U16(data, b + 0x15 + 2 * k) / 100.0, U16(data, b + 0x35 + 2 * k) / 100.0);
            channels[i] = new(i + 1, data[b], S16(data, b + 3), U16(data, rec + 1) / 100.0,
                U16(data, rec + 3) / 100.0, U16(data, rec + 5) / 100.0,
                (data[rec] & 1) != 0, (data[rec] & 2) != 0, U16(data, b + 0x13) / 100.0, points);
        }
        return new(U16(data, 1), U16(data, 6), S16(data, 8) / 100.0, channels, data[0x3BD]);
    }
}
