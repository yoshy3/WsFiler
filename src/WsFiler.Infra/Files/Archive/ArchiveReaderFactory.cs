using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.Xz;

namespace WsFiler.Infra.Files.Archive;

internal static class ArchiveReaderFactory
{
    private static readonly IReadOnlyList<(string Extension, Func<IArchiveReader> Create)> Readers =
    [
        (".tar.gz", () => new SharpCompressArchiveReader(OpenTarGz)),
        (".tar.bz2", () => new SharpCompressArchiveReader(OpenTarBz2)),
        (".tar.xz", () => new SharpCompressArchiveReader(OpenTarXz)),
        (".tgz", () => new SharpCompressArchiveReader(OpenTarGz)),
        (".tbz2", () => new SharpCompressArchiveReader(OpenTarBz2)),
        (".txz", () => new SharpCompressArchiveReader(OpenTarXz)),
        (".tar", () => new SharpCompressArchiveReader(static path => TarArchive.Open(path))),
        (".7z", () => new SharpCompressArchiveReader(static path => SevenZipArchive.Open(path))),
        (".rar", () => new SharpCompressArchiveReader(static path => RarArchive.Open(path))),
        (".zip", () => new ZipArchiveReaderAdapter()),
        (".gz", () => new SingleFileCompressionReader(
            static stream => new GZipStream(stream, CompressionMode.Decompress),
            ".gz")),
        (".bz2", () => new SingleFileCompressionReader(
            static stream => new BZip2Stream(stream, CompressionMode.Decompress, decompressConcatenated: true),
            ".bz2")),
        (".xz", () => new SingleFileCompressionReader(
            static stream => new XZStream(stream),
            ".xz")),
    ];

    public static IReadOnlyList<string> SupportedExtensions { get; } =
        Readers.Select(pair => pair.Extension).ToArray();

    public static IArchiveReader GetReader(string archiveFilePath)
    {
        foreach (var (extension, create) in Readers)
        {
            if (archiveFilePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return create();
            }
        }

        throw new NotSupportedException($"Unsupported archive format: {archiveFilePath}");
    }

    private static IArchive OpenTarGz(string path)
    {
        using var stream = File.OpenRead(path);
        using var decompressed = new GZipStream(stream, CompressionMode.Decompress);
        return TarArchive.Open(BufferToSeekableStream(decompressed));
    }

    private static IArchive OpenTarBz2(string path)
    {
        using var stream = File.OpenRead(path);
        using var decompressed = new BZip2Stream(stream, CompressionMode.Decompress, decompressConcatenated: true);
        return TarArchive.Open(BufferToSeekableStream(decompressed));
    }

    private static IArchive OpenTarXz(string path)
    {
        using var stream = File.OpenRead(path);
        using var decompressed = new XZStream(stream);
        return TarArchive.Open(BufferToSeekableStream(decompressed));
    }

    private static MemoryStream BufferToSeekableStream(Stream source)
    {
        var memory = new MemoryStream();
        source.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }
}
