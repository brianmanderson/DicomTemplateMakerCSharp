using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DicomTemplateMakerGUI.DicomTemplateServices
{
    /// <summary>
    /// The names, sizes and last-write times of the input files of one directory (not recursive), used to
    /// tell whether it changed between scans. Generated RT files are left out, so writing an RT into a
    /// directory does not make it look changed. The runner remembers only <see cref="Hash"/>.
    /// </summary>
    internal sealed class DirectoryFingerprint
    {
        private DirectoryFingerprint(string directory, IReadOnlyList<string> files, string hash, bool hasDicomFiles, DateTime newestUtc)
        {
            Directory = directory;
            Files = files;
            Hash = hash;
            HasDicomFiles = hasDicomFiles;
            NewestUtc = newestUtc;
        }

        public string Directory { get; }

        /// <summary>Full paths of the input files, in ordinal name order.</summary>
        public IReadOnlyList<string> Files { get; }

        /// <summary>SHA-256 of the names, sizes and last-write times: equal hashes mean an unchanged directory.</summary>
        public string Hash { get; }

        /// <summary>True when at least one input file has the .dcm extension; other directories are not processed.</summary>
        public bool HasDicomFiles { get; }

        /// <summary>
        /// The latest last-write or creation time of the input files (UTC). The creation time counts because a
        /// copy keeps the source's last-write time on Windows but gets a new creation time.
        /// </summary>
        public DateTime NewestUtc { get; }

        /// <summary>Lists <paramref name="directory"/>, leaving out files for which <paramref name="isExcluded"/> (given the file name) is true.</summary>
        public static DirectoryFingerprint Take(string directory, Func<string, bool> isExcluded)
        {
            var files = new List<FileInfo>();
            foreach (FileInfo file in new DirectoryInfo(directory).EnumerateFiles())
            {
                if (!isExcluded(file.Name))
                {
                    files.Add(file);
                }
            }

            files.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Span<byte> numbers = stackalloc byte[20];
            DateTime newest = DateTime.MinValue;
            bool hasDicom = false;
            foreach (FileInfo file in files)
            {
                DateTime written = file.LastWriteTimeUtc;
                DateTime created = file.CreationTimeUtc;
                byte[] name = Encoding.UTF8.GetBytes(file.Name);
                BinaryPrimitives.WriteInt32LittleEndian(numbers, name.Length);
                BinaryPrimitives.WriteInt64LittleEndian(numbers.Slice(4), file.Length);
                BinaryPrimitives.WriteInt64LittleEndian(numbers.Slice(12), written.Ticks);
                hash.AppendData(numbers);
                hash.AppendData(name);
                DateTime latest = written > created ? written : created;
                if (latest > newest)
                {
                    newest = latest;
                }

                hasDicom |= string.Equals(file.Extension, ".dcm", StringComparison.OrdinalIgnoreCase);
            }

            return new DirectoryFingerprint(directory, files.Select(f => f.FullName).ToList(), Convert.ToHexString(hash.GetHashAndReset()), hasDicom, newest);
        }
    }
}
