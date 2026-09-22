using System.IO;
using System.IO.Compression;
using System.Text;

namespace ShowVersionNum;

internal static class ZipVersionReader
{
    private const string MissingVersionMessage = "该压缩包没有version文件";

    public static string ReadVersionText(string zipPath)
    {
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry? versionEntry = FindVersionEntry(archive);
            if (versionEntry is null)
            {
                return MissingVersionMessage;
            }

            using Stream stream = versionEntry.Open();
            using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (InvalidDataException)
        {
            return "无法读取压缩包：文件不是有效的zip压缩包。";
        }
        catch (IOException ex)
        {
            return $"无法读取压缩包：{ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"无法读取压缩包：{ex.Message}";
        }
    }

    private static ZipArchiveEntry? FindVersionEntry(ZipArchive archive)
    {
        ZipArchiveEntry? rootVersion = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.Equals("version", StringComparison.OrdinalIgnoreCase));

        return rootVersion ?? archive.Entries.FirstOrDefault(entry =>
            entry.Name.Equals("version", StringComparison.OrdinalIgnoreCase));
    }
}
