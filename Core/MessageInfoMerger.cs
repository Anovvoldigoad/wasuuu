using System.Buffers.Binary;
using System.IO.Compression;
using LegacyBinaryReader = NSC_ModManager.BinaryReader;

namespace NSC_ModManager_Android.Core;

/// <summary>
/// Portable messageInfo.bin.xfbin parser/merger/serializer.
/// This intentionally contains no WPF/editor code. Merge behavior follows
/// NSC ModManager 2.1.1.0, but processes one target language at a time so the
/// Android app does not keep all 15 large vanilla localization tables in RAM.
/// </summary>
internal static class MessageInfoMerger
{
    internal static readonly string[] NscLanguages =
    {
        "arae", "chi", "eng", "esmx", "fre", "ger", "idid", "ita",
        "kokr", "pol", "por", "rus", "spa", "thth", "zhcn"
    };

    internal static readonly string[] Ns4Languages =
    {
        "arae", "chi", "eng", "esmx", "fre", "ger", "ita", "kokr",
        "pol", "por", "rus", "spa"
    };

    internal sealed class Record
    {
        public byte[] Crc32 { get; init; } = new byte[4] { 0xFF, 0xFF, 0xFF, 0xFF };
        public byte[] MainText { get; init; } = Array.Empty<byte>();
        public byte[] SecondaryText { get; init; } = Array.Empty<byte>();
        public byte[] Speaker { get; init; } = Array.Empty<byte>();
        public short AcbFileId { get; init; }
        public short CueId { get; init; }
        public bool DisableText { get; init; }

        public Record Clone(bool clearSpeaker = false) => new()
        {
            Crc32 = (byte[])Crc32.Clone(),
            MainText = (byte[])MainText.Clone(),
            SecondaryText = (byte[])SecondaryText.Clone(),
            Speaker = clearSpeaker ? Array.Empty<byte>() : (byte[])Speaker.Clone(),
            AcbFileId = AcbFileId,
            CueId = CueId,
            DisableText = DisableText,
        };
    }

    internal sealed record Contribution(string MessageRoot, string StormVersion, string OwnerLabel);

    internal sealed class State
    {
        internal State(string baselineRoot) => BaselineRoot = baselineRoot;
        internal string BaselineRoot { get; }
        internal List<Contribution> Contributions { get; } = new();
        internal HashSet<string> SourceFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal readonly record struct SaveStats(
        int SourceFilesDetected,
        int TargetLanguageMerges,
        int EntriesAppended,
        int OutputsGenerated,
        int MissingSourceMappings,
        IReadOnlyList<string> Details);

    internal static State LoadNscBaseline(string messageBaseZip, string workRoot)
    {
        if (!File.Exists(messageBaseZip))
            throw new FileNotFoundException("Bundled NSC message baseline is missing.", messageBaseZip);

        string seed = Path.Combine(workRoot, "message_seed");
        if (Directory.Exists(seed)) Directory.Delete(seed, true);
        Directory.CreateDirectory(seed);
        ZipFile.ExtractToDirectory(messageBaseZip, seed, true);

        string root = Path.Combine(seed, "NSC", "message", "WIN64");
        foreach (string lang in NscLanguages)
        {
            string path = Path.Combine(root, lang, "messageInfo.bin.xfbin");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Message baseline missing language '{lang}'.", path);
        }
        return new State(root);
    }

    internal static bool QueueDirectory(State state, string messageRoot, string stormVersion, string ownerLabel)
    {
        if (!Directory.Exists(messageRoot)) return false;

        bool found = false;
        foreach (string file in Directory.EnumerateFiles(messageRoot, "messageInfo.bin.xfbin", SearchOption.AllDirectories))
        {
            state.SourceFiles.Add(file);
            found = true;
        }
        if (!found) return false;

        state.Contributions.Add(new Contribution(messageRoot, stormVersion, ownerLabel));
        return true;
    }

    internal static SaveStats Save(State state, string paramFilesRoot)
    {
        var details = new List<string>();
        int targetMerges = 0;
        int entries = 0;
        int written = 0;
        int missingSources = 0;

        foreach (string targetLang in NscLanguages)
        {
            string baseline = Path.Combine(state.BaselineRoot, targetLang, "messageInfo.bin.xfbin");
            List<Record> records = ParseNsc(baseline);

            foreach (Contribution contribution in state.Contributions)
            {
                bool ns4 = contribution.StormVersion.Equals("NS4", StringComparison.OrdinalIgnoreCase);
                string sourceLang;
                if (!ns4)
                {
                    sourceLang = targetLang;
                }
                else if (Ns4Languages.Contains(targetLang, StringComparer.OrdinalIgnoreCase))
                {
                    sourceLang = targetLang;
                }
                else
                {
                    // Desktop-compatible NS4 -> NSC fallback.
                    sourceLang = targetLang.Equals("zhcn", StringComparison.OrdinalIgnoreCase) ? "chi" : "eng";
                }

                string? source = FindLanguageFile(contribution.MessageRoot, sourceLang);
                if (source is null)
                {
                    missingSources++;
                    details.Add($"{contribution.OwnerLabel}: missing {(ns4 ? "NS4" : "NSC")} source language {sourceLang} for NSC target {targetLang}");
                    continue;
                }

                List<Record> parsed = ns4 ? ParseNs4(source) : ParseNsc(source);
                records.AddRange(parsed.Select(r => r.Clone(clearSpeaker: ns4)));
                targetMerges++;
                entries += parsed.Count;
                details.Add($"{contribution.OwnerLabel}: message {sourceLang}->{targetLang} +{parsed.Count}");
            }

            string dir = Path.Combine(paramFilesRoot, "data", "message", "WIN64", targetLang);
            Directory.CreateDirectory(dir);
            string output = Path.Combine(dir, "messageInfo.bin.xfbin");
            File.WriteAllBytes(output, SerializeNsc(targetLang, records));
            if (!File.Exists(output) || new FileInfo(output).Length < 128)
                throw new InvalidDataException("messageInfo serialization failed: " + output);
            written++;
        }

        return new SaveStats(state.SourceFiles.Count, targetMerges, entries, written, missingSources, details);
    }

    private static string? FindLanguageFile(string messageRoot, string lang)
    {
        string expected = Path.Combine(messageRoot, "WIN64", lang, "messageInfo.bin.xfbin");
        if (File.Exists(expected)) return expected;

        // Be tolerant of casing/layout differences used by community mods,
        // but still require the language directory and exact message filename.
        foreach (string file in Directory.EnumerateFiles(messageRoot, "messageInfo.bin.xfbin", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (normalized.Contains($"/WIN64/{lang}/", StringComparison.OrdinalIgnoreCase))
                return file;
        }
        return null;
    }

    private static List<Record> ParseNsc(string path) => Parse(path, isNs4: false);
    private static List<Record> ParseNs4(string path) => Parse(path, isNs4: true);

    private static List<Record> Parse(string path, bool isNs4)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 0x60 || data[0] != (byte)'N' || data[1] != (byte)'U' || data[2] != (byte)'C' || data[3] != (byte)'C')
            throw new InvalidDataException("Invalid messageInfo XFBIN: " + path);

        int chunkTable = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4));
        int start = checked(0x44 + chunkTable);
        Ensure(data, start, 0x10, path);
        int count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(start + 4, 4));
        int stride = isNs4 ? 0x28 : 0x30;
        if (count < 0 || count > 1_000_000)
            throw new InvalidDataException($"Invalid messageInfo entry count {count}: {path}");
        Ensure(data, start + 0x10, checked(count * stride), path);

        var result = new List<Record>(count);
        for (int i = 0; i < count; i++)
        {
            int ptr = start + 0x10 + i * stride;
            byte[] crc = data.AsSpan(ptr, 4).ToArray();
            byte[] speaker;
            byte[] secondary;
            byte[] main;
            short acb;
            short cue;
            bool disable;

            if (isNs4)
            {
                speaker = Array.Empty<byte>();
                secondary = ReadRelativeString(data, ptr, 0x08, path);
                main = ReadRelativeString(data, ptr, 0x10, path);
                acb = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(ptr + 0x1E, 2));
                cue = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(ptr + 0x20, 2));
                disable = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(ptr + 0x22, 2)) == 1;
            }
            else
            {
                speaker = ReadRelativeString(data, ptr, 0x08, path);
                secondary = ReadRelativeString(data, ptr, 0x10, path);
                main = ReadRelativeString(data, ptr, 0x18, path);
                acb = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(ptr + 0x26, 2));
                cue = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(ptr + 0x28, 2));
                disable = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(ptr + 0x2A, 2)) == 1;
            }

            result.Add(new Record
            {
                Crc32 = crc,
                Speaker = speaker,
                SecondaryText = secondary,
                MainText = main,
                AcbFileId = acb,
                CueId = cue,
                DisableText = disable,
            });
        }
        return result;
    }

    private static byte[] ReadRelativeString(byte[] data, int recordPtr, int fieldOffset, string path)
    {
        int rel = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(recordPtr + fieldOffset, 4));
        if (rel == 0) return Array.Empty<byte>();
        int start = checked(recordPtr + fieldOffset + rel);
        if ((uint)start >= (uint)data.Length)
            throw new InvalidDataException($"messageInfo string pointer out of range: {path}");
        int end = start;
        int max = Math.Min(data.Length, start + 65536);
        while (end < max && data[end] != 0) end++;
        return data.AsSpan(start, end - start).ToArray();
    }

    private static void Ensure(byte[] data, int offset, int count, string path)
    {
        if (offset < 0 || count < 0 || offset > data.Length - count)
            throw new InvalidDataException($"messageInfo structure out of range: {path}");
    }

    // Serializer ported from NSC ModManager 2.1.1.0 MessageInfoViewModel.ConvertToFile,
    // stripped of every WPF/UI dependency.
    private static byte[] SerializeNsc(string lang, IReadOnlyList<Record> records)
    {
        byte[] fileBytes = new byte[127]
        {
            0x4E,0x55,0x43,0x43,0x00,0x00,0x00,0x79,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
            0x00,0x00,0x80,0xBC,0x00,0x00,0x00,0x03,0x00,0x79,0x00,0x00,0x00,0x00,0x00,0x04,
            0x00,0x00,0x00,0x3B,0x00,0x00,0x01,0x49,0x00,0x00,0x4C,0xE3,0x00,0x00,0x01,0x4B,
            0x00,0x00,0x0F,0x6F,0x00,0x00,0x01,0x4B,0x00,0x00,0x0F,0x84,0x00,0x00,0x05,0x20,
            0x00,0x00,0x00,0x00,0x6E,0x75,0x63,0x63,0x43,0x68,0x75,0x6E,0x6B,0x4E,0x75,0x6C,
            0x6C,0x00,0x6E,0x75,0x63,0x63,0x43,0x68,0x75,0x6E,0x6B,0x42,0x69,0x6E,0x61,0x72,
            0x79,0x00,0x6E,0x75,0x63,0x63,0x43,0x68,0x75,0x6E,0x6B,0x50,0x61,0x67,0x65,0x00,
            0x6E,0x75,0x63,0x63,0x43,0x68,0x75,0x6E,0x6B,0x49,0x6E,0x64,0x65,0x78,0x00
        };

        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[1]);
        fileBytes = LegacyBinaryReader.b_AddString(fileBytes, "WIN64/" + lang + "/messageInfo.bin");
        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[1]);
        int ptrPath = fileBytes.Length;
        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[1]);
        fileBytes = LegacyBinaryReader.b_AddString(fileBytes, "messageInfo");
        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[1]);
        fileBytes = LegacyBinaryReader.b_AddString(fileBytes, "Page0");
        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[1]);
        fileBytes = LegacyBinaryReader.b_AddString(fileBytes, "index");
        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[1]);
        int ptrName = fileBytes.Length;
        int addedBytes = 0;
        while (fileBytes.Length % 4 != 0)
        {
            addedBytes++;
            fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[1]);
        }

        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[48]
        {
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,0,1,0,0,0,1,0,0,0,2,0,0,0,0,
            0,0,0,2,0,0,0,3,0,0,0,0,0,0,0,3
        });
        int ptrSection = fileBytes.Length;
        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[16] { 0,0,0,0,0,0,0,1,0,0,0,2,0,0,0,3 });

        int totalLength = fileBytes.Length;
        int pathLength = ptrPath - 127;
        int nameLength = ptrName - ptrPath;
        int section1Length = ptrSection - ptrName - addedBytes;
        int fullLength = totalLength - 68 + 40;
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(fullLength), 16, 1);
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(2), 36, 1);
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(pathLength), 40, 1);
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(4), 44, 1);
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(nameLength), 48, 1);
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(4), 52, 1);
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(section1Length), 56, 1);
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(4), 60, 1);

        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[40]
        {
            0,0,0,0,0,0,0,0,0,0x79,0,0,0,0,0,0,0,0,0,0,0,0x79,0,0,0,0,0,0,0,0,0,1,
            0,0x79,0,0,0,0,0,0
        });
        int size1Index = fileBytes.Length - 0x10;
        int size2Index = fileBytes.Length - 0x4;
        int countIndex = fileBytes.Length + 0x4;
        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[0x10] { 0xE9,0x03,0,0,0,0,0,0,0x08,0,0,0,0,0,0,0 });
        int startPtr = fileBytes.Length;

        var file = new List<byte>(checked(records.Count * 0x30 + 4096));
        for (int i = 0; i < records.Count * 0x30; i++) file.Add(0);
        var speakerPointers = new List<int>(records.Count);
        var secondaryPointers = new List<int>(records.Count);
        var mainPointers = new List<int>(records.Count);

        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            byte[] speaker = record.Speaker ?? Array.Empty<byte>();
            byte[] secondary = record.SecondaryText ?? Array.Empty<byte>();
            byte[] main = record.MainText ?? Array.Empty<byte>();

            speakerPointers.Add(file.Count);
            if (speaker.Length > 0) { file.AddRange(speaker); file.Add(0); }
            secondaryPointers.Add(file.Count);
            if (secondary.Length > 0) { file.AddRange(secondary); file.Add(0); }
            mainPointers.Add(file.Count);
            if (main.Length > 0) { file.AddRange(main); file.Add(0); }

            int row = i * 0x30;
            if (speaker.Length > 0) WriteInt32(file, row + 0x08, speakerPointers[i] - row - 0x08);
            if (secondary.Length > 0) WriteInt32(file, row + 0x10, secondaryPointers[i] - row - 0x10);
            if (main.Length > 0) WriteInt32(file, row + 0x18, mainPointers[i] - row - 0x18);

            byte[] crc = record.Crc32 is { Length: >= 4 } ? record.Crc32 : new byte[4] { 0xFF,0xFF,0xFF,0xFF };
            for (int b = 0; b < 4; b++) file[row + b] = crc[b];
            file[row + 0x24] = 0xFF;
            file[row + 0x25] = 0xFF;
            WriteInt16(file, row + 0x26, record.AcbFileId);
            WriteInt16(file, row + 0x28, record.CueId);
            file[row + 0x2A] = record.DisableText ? (byte)1 : (byte)0;
        }

        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, file.ToArray());
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(fileBytes.Length - startPtr + 0x14), size1Index, 1);
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(fileBytes.Length - startPtr + 0x10), size2Index, 1);
        fileBytes = LegacyBinaryReader.b_ReplaceBytes(fileBytes, BitConverter.GetBytes(records.Count), countIndex);
        fileBytes = LegacyBinaryReader.b_AddBytes(fileBytes, new byte[20]
        {
            0,0,0,8,0,0,0,2,0,0x79,0x21,0x77,0,0,0,4,0,0,0,0
        });
        return fileBytes;
    }

    private static void WriteInt32(List<byte> file, int offset, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 4; i++) file[offset + i] = bytes[i];
    }

    private static void WriteInt16(List<byte> file, int offset, short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        file[offset] = bytes[0];
        file[offset + 1] = bytes[1];
    }
}
