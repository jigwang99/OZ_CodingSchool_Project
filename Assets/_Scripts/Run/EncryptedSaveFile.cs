using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AutoBattler.Run
{
    internal enum SaveFileKind : byte { Run = 1, Campaign = 2 }

    /// <summary>Windows 사용자 범위로 저장 파일을 보호하고 평문 저장을 이전.</summary>
    internal static class EncryptedSaveFile
    {
        private static readonly byte[] Header = { (byte)'P', (byte)'T', (byte)'S', (byte)'V', 1 };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private const int MaximumProtectionOverhead = 64 * 1024;

        internal static bool Exists(string path) => File.Exists(path) || File.Exists(LegacyPath(path));
        private static string LegacyPath(string path) => Path.ChangeExtension(path, ".json");

        internal static string ReadJson(string path, SaveFileKind kind, int limit, out bool legacy)
        {
            try
            {
                legacy = false;
                return ReadProtectedJson(path, kind, limit);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }

            legacy = true;
            return ReadLegacyJson(LegacyPath(path), limit);
        }

        private static string ReadProtectedJson(string path, SaveFileKind kind, int limit)
        {
            byte[] file = ReadBytes(path, limit + MaximumProtectionOverhead);
            if (file.Length <= Header.Length || !file.AsSpan(0, Header.Length).SequenceEqual(Header))
                throw new IOException("지원하지 않는 암호화 저장 형식.");

            byte[] plaintext = Protect(file.AsSpan(Header.Length).ToArray(), decrypt: true);
            try
            {
                if (plaintext.Length <= 1 || plaintext.Length - 1 > limit || plaintext[0] != (byte)kind)
                    throw new IOException("암호화 저장의 데이터 종류 또는 크기 오류.");
                return Utf8.GetString(plaintext, 1, plaintext.Length - 1);
            }
            finally { Array.Clear(plaintext, 0, plaintext.Length); }
        }

        private static string ReadLegacyJson(string path, int limit)
        {
            using StreamReader reader = new StreamReader(new MemoryStream(ReadBytes(path, limit)), Utf8);
            return reader.ReadToEnd();
        }

        private static byte[] ReadBytes(string path, int limit)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            long length = stream.Length;
            if (length <= 0 || length > limit) throw new IOException("저장 파일 크기 오류.");
            using BinaryReader reader = new BinaryReader(stream);
            byte[] bytes = reader.ReadBytes((int)length);
            if (bytes.Length != length) throw new EndOfStreamException("저장 파일이 잘렸습니다.");
            return bytes;
        }

        internal static void WriteJson(string path, SaveFileKind kind, int limit, string json, bool replaceExisting = true)
        {
            int jsonLength = Utf8.GetByteCount(json);
            if (jsonLength <= 0 || jsonLength > limit) throw new IOException("저장 데이터 크기 오류.");
            byte[] plaintext = new byte[jsonLength + 1];
            byte[] encrypted;
            try
            {
                // 파일 종류까지 암호화해 다른 저장 파일의 내용으로 바뀌는 것을 감지.
                plaintext[0] = (byte)kind;
                Utf8.GetBytes(json, 0, json.Length, plaintext, 1);
                encrypted = Protect(plaintext, decrypt: false);
            }
            finally { Array.Clear(plaintext, 0, plaintext.Length); }
            if (Header.Length + encrypted.Length > limit + MaximumProtectionOverhead)
                throw new IOException("암호화 저장 파일 크기 오류.");

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // 같은 폴더의 임시 파일에 암호문을 완성한 뒤 교체.
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(Header, 0, Header.Length);
                    stream.Write(encrypted, 0, encrypted.Length);
                    stream.Flush(true);
                }
                if (replaceExisting && File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        internal static void MigrateLegacy(string path, SaveFileKind kind, int limit, string json, bool legacy)
        {
            string oldPath = LegacyPath(path);
            if (!File.Exists(oldPath)) return;
            if (legacy)
            {
                WriteJson(path, kind, limit, json, replaceExisting: false);
                if (!string.Equals(ReadProtectedJson(path, kind, limit), json, StringComparison.Ordinal))
                    throw new IOException("이전한 저장 파일의 복호화 검증 실패.");
            }
            // 정리 도중 중단되면 다음 불러오기에서 같은 원본인지 확인하고 재시도.
            if (!string.Equals(ReadLegacyJson(oldPath, limit), json, StringComparison.Ordinal)) return;
            File.Delete(oldPath + ".tmp");
            File.Delete(oldPath + ".bak");
            File.Delete(oldPath);
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private const uint CryptProtectUiForbidden = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Length;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(ref DataBlob input, IntPtr description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);

        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        private static byte[] Protect(byte[] data, bool decrypt)
        {
            GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            DataBlob output = default;
            try
            {
                DataBlob input = new DataBlob { Length = data.Length, Data = pin.AddrOfPinnedObject() };
                bool success = decrypt
                    ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output)
                    : CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output);
                if (!success) throw new CryptographicException(Marshal.GetLastWin32Error());
                if (output.Data == IntPtr.Zero || output.Length <= 0) throw new CryptographicException("DPAPI 결과가 비어 있습니다.");
                byte[] result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
                pin.Free();
            }
        }
#else
        private static byte[] Protect(byte[] data, bool decrypt)
            => throw new PlatformNotSupportedException("저장 암호화는 Windows 빌드에서 지원됩니다.");
#endif
    }
}
