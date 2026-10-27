using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace TrailmakersSurroundUI
{
    internal static class Payload
    {
        public static byte[] Read()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("surround-ui.addon64"))
            using (MemoryStream buffer = new MemoryStream())
            {
                if (stream == null) throw new InvalidDataException("The embedded add-on is missing.");
                stream.CopyTo(buffer);
                byte[] bytes = buffer.ToArray();
                if (!String.Equals(Hash(bytes), PayloadInfo.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The installer payload failed its integrity check. Download a fresh release.");
                return bytes;
            }
        }

        public static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        public static string FileHash(string path) { return Hash(File.ReadAllBytes(path)); }
    }

    [DataContract]
    internal sealed class InstallRecord
    {
        [DataMember] public string Product = "TrailmakersSurroundUI";
        [DataMember] public int Format = 1;
        [DataMember] public string Version;
        [DataMember] public string Sha256;
        [DataMember] public string InstalledUtc;
    }

    internal static class InstallerCore
    {
        public const string AddonName = "surround-ui.addon64";
        public const string RecordName = ".surround-ui-install.json";
        public const string ProjectUrl = "https://github.com/Gibsondude78/Trailmakers-Surround-UI-Fix";

        public static string GameFolder(string input)
        {
            if (String.IsNullOrWhiteSpace(input)) throw new InvalidOperationException("Choose your Trailmakers installation folder.");
            string root = Path.GetFullPath(input.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, "Trailmakers.exe")) ||
                !File.Exists(Path.Combine(root, "GameAssembly.dll")))
                throw new InvalidOperationException("Choose the folder containing Trailmakers.exe and GameAssembly.dll. In Steam: Manage > Browse local files.");
            if (String.Equals(root, Path.GetPathRoot(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Please use a dedicated game folder, not a drive root.");
            return root;
        }

        private static void PlainFile(string path)
        {
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("The add-on or its installation record is a symbolic link. Resolve it manually before continuing.");
        }

        public static bool GameRunning(string root)
        {
            foreach (Process process in Process.GetProcessesByName("Trailmakers"))
            using (process)
            {
                try
                {
                    if (String.Equals(Path.GetDirectoryName(process.MainModule.FileName), root,
                        StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { return true; }
            }
            return false;
        }

        private static void Closed(string root)
        {
            if (GameRunning(root)) throw new InvalidOperationException("Close Trailmakers before installing or uninstalling this fix.");
        }

        private static InstallRecord Record(string root)
        {
            string path = Path.Combine(root, RecordName);
            PlainFile(path);
            if (!File.Exists(path)) return null;
            try
            {
                using (FileStream file = File.OpenRead(path))
                {
                    InstallRecord record = (InstallRecord)new DataContractJsonSerializer(typeof(InstallRecord)).ReadObject(file);
                    if (record.Product != "TrailmakersSurroundUI" || record.Format != 1 ||
                        record.Sha256 == null || !Regex.IsMatch(record.Sha256, "^[a-fA-F0-9]{64}$"))
                        throw new InvalidDataException();
                    return record;
                }
            }
            catch (Exception error)
            {
                if (error is IOException || error is SerializationException || error is InvalidDataException)
                    throw new InvalidOperationException("The existing installation record is invalid. No add-on files were changed.");
                throw;
            }
        }

        public static string Loader(string root)
        {
            foreach (string name in new[] { "dxgi.dll", "d3d11.dll" })
            {
                string path = Path.Combine(root, name);
                if (File.Exists(path) && PeExports.HasReShade(path)) return path;
            }
            return null;
        }

        public static void Install(string input)
        {
            byte[] payload = Payload.Read();
            string root = GameFolder(input);
            Closed(root);
            if (Loader(root) == null)
                throw new InvalidOperationException("ReShade wasn't found. Install ReShade 6.8 or newer with full add-on support for Trailmakers (DirectX 10/11/12), then try again.");
            string target = Path.Combine(root, AddonName);
            PlainFile(target);
            InstallRecord existing = Record(root);
            if (File.Exists(target))
            {
                string hash = Payload.FileHash(target);
                if (existing != null && !String.Equals(hash, existing.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The installed add-on has changed since installation. It was left untouched.");
                if (!String.Equals(hash, PayloadInfo.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(existing == null
                        ? "A different surround-ui.addon64 already exists. It was left untouched."
                        : "An older version is installed. Remove it, then install this version. Your settings will be kept.");
            }
            string stage = Path.Combine(root, ".surround-ui-" + Guid.NewGuid().ToString("N") + ".tmp");
            bool added = false;
            try
            {
                if (!File.Exists(target))
                {
                    using (FileStream file = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { file.Write(payload, 0, payload.Length); file.Flush(true); }
                    File.Move(stage, target);
                    added = true;
                }
                WriteRecord(root, new InstallRecord { Version = PayloadInfo.Version,
                    Sha256 = PayloadInfo.Sha256, InstalledUtc = DateTime.UtcNow.ToString("o") });
            }
            catch
            {
                if (added && File.Exists(target) && Payload.FileHash(target) == PayloadInfo.Sha256) File.Delete(target);
                throw;
            }
            finally { if (File.Exists(stage)) File.Delete(stage); }
        }

        private static void WriteRecord(string root, InstallRecord record)
        {
            string target = Path.Combine(root, RecordName);
            string stage = Path.Combine(root, ".surround-ui-record-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream file = new FileStream(stage, FileMode.CreateNew))
                { new DataContractJsonSerializer(typeof(InstallRecord)).WriteObject(file, record); file.Flush(true); }
                if (File.Exists(target)) File.Replace(stage, target, null);
                else File.Move(stage, target);
            }
            finally { if (File.Exists(stage)) File.Delete(stage); }
        }

        public static void Uninstall(string input)
        {
            string root = GameFolder(input);
            Closed(root);
            string target = Path.Combine(root, AddonName);
            PlainFile(target);
            InstallRecord record = Record(root);
            if (File.Exists(target))
            {
                string expected = record == null ? PayloadInfo.Sha256 : record.Sha256;
                if (!String.Equals(Payload.FileHash(target), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The add-on file does not match the recorded installation. It was left untouched.");
                File.Delete(target);
            }
            string recordFile = Path.Combine(root, RecordName);
            if (record != null && File.Exists(recordFile)) File.Delete(recordFile);
            // Keep ReShade, other add-ons, game files, and this fix's INI/log.
        }

        public static IEnumerable<string> DetectFolders()
        {
            HashSet<string> libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string steam = null;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                if (key != null) steam = key.GetValue("SteamPath") as string;
            if (String.IsNullOrEmpty(steam)) steam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            libraries.Add(steam);
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
            foreach (string library in libraries)
            {
                string directory = "Trailmakers";
                string manifest = Path.Combine(library, "steamapps", "appmanifest_585420.acf");
                if (File.Exists(manifest))
                {
                    Match match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                    if (match.Success) directory = match.Groups[1].Value;
                }
                string path = Path.Combine(library, "steamapps", "common", directory);
                if (File.Exists(Path.Combine(path, "Trailmakers.exe"))) yield return path;
            }
        }
    }

    internal static class PeExports
    {
        // Read PE metadata only. Never load or execute a DLL selected by a user.
        public static bool HasReShade(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 256 || BitConverter.ToUInt16(bytes, 0) != 0x5a4d) return false;
                int pe = BitConverter.ToInt32(bytes, 0x3c);
                if (pe < 64 || pe + 264 > bytes.Length || BitConverter.ToUInt32(bytes, pe) != 0x4550 ||
                    BitConverter.ToUInt16(bytes, pe + 4) != 0x8664) return false;
                int optional = pe + 24;
                if (BitConverter.ToUInt16(bytes, optional) != 0x20b) return false;
                uint rva = BitConverter.ToUInt32(bytes, optional + 112);
                int sections = BitConverter.ToUInt16(bytes, pe + 6);
                int sectionStart = optional + BitConverter.ToUInt16(bytes, pe + 20);
                Func<uint, int> offset = delegate(uint address)
                {
                    for (int i = 0; i < sections; ++i)
                    {
                        int s = checked(sectionStart + i * 40);
                        uint start = BitConverter.ToUInt32(bytes, s + 12);
                        uint length = Math.Max(BitConverter.ToUInt32(bytes, s + 8), BitConverter.ToUInt32(bytes, s + 16));
                        if (address >= start && (ulong)address < (ulong)start + length)
                            return checked((int)((long)BitConverter.ToUInt32(bytes, s + 20) + address - start));
                    }
                    throw new InvalidDataException();
                };
                int exports = offset(rva);
                uint count = BitConverter.ToUInt32(bytes, exports + 24);
                if (count > 100000) return false;
                int names = offset(BitConverter.ToUInt32(bytes, exports + 32));
                bool register = false, unregister = false, perAddon = false;
                for (int i = 0; i < count; ++i)
                {
                    int start = offset(BitConverter.ToUInt32(bytes, checked(names + i * 4)));
                    int end = start;
                    while (end < bytes.Length && end - start <= 256 && bytes[end] != 0) ++end;
                    if (end >= bytes.Length || end - start > 256) continue;
                    string name = Encoding.ASCII.GetString(bytes, start, end - start);
                    register |= name == "ReShadeRegisterAddon";
                    unregister |= name == "ReShadeUnregisterAddon";
                    perAddon |= name == "ReShadeRegisterEventForAddon";
                }
                return register && unregister && perAddon;
            }
            catch (Exception error)
            {
                if (error is IOException || error is ArgumentException || error is OverflowException || error is InvalidDataException)
                    return false;
                throw;
            }
        }
    }
}
