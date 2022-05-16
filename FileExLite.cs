//--------------------------------------------------------------------------
// <summary>
//   
// </summary>
// <copyright file="FileExLite.cs" company="Chuck Hill">
// Copyright (c) 2020 Chuck Hill.
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public License
// as published by the Free Software Foundation; either version 2.1
// of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// The GNU Lesser General Public License can be viewed at
// http://www.opensource.org/licenses/lgpl-license.php. If
// you unfamiliar with this license or have questions about
// it, here is an http://www.gnu.org/licenses/gpl-faq.html.
//
// All code and executables are provided "as is" with no warranty
// either express or implied. The author accepts no liability for
// any damage or loss of business that this product may cause.
// </copyright>
// <repository>https://github.com/ChuckHill2/DownloadiingTest</repository>
// <author>Chuck Hill</author>
//--------------------------------------------------------------------------
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DownloadingTest
{
    public static class FileEx
    {
        #region Win32
        /// <summary>
        /// This is a low-level alternative to:
        ///    • System.IO.File.GetCreationTime()
        ///    • System.IO.File.GetLastWriteTime()
        ///    • System.IO.File.GetLastAccessTime()
        ///    and
        ///    • System.IO.File.SetCreationTime()
        ///    • System.IO.File.SetLastWriteTime()
        ///    • System.IO.File.SetLastAccessTime()
        /// The reason is sometimes some fields do not get set properly. File open/close 3 times in rapid succession?
        /// </summary>
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, ref long lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, IntPtr creationTime, ref long lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, IntPtr lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, ref long lastAccessTime, IntPtr lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, IntPtr creationTime, IntPtr lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, IntPtr lastAccessTime, IntPtr lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, IntPtr creationTime, ref long lastAccessTime, IntPtr lastWriteTime);

        [DllImport("kernel32.dll", SetLastError = true, BestFitMapping = false)]
        private static extern bool GetFileTime(IntPtr hFile, out long creationTime, out long lastAccessTime, out long lastWriteTime);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true, BestFitMapping = false)]
        private static extern bool CloseHandle(IntPtr hFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern bool DeleteFile(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern bool CopyFile(string srcfile, string dstfile, bool failIfExists);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern bool MoveFileEx(string src, string dst, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, BestFitMapping = false)]
        private static extern bool GetFileSizeEx(IntPtr hFile, out long lpFileSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern int GetFileAttributes(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern bool GetFileAttributesEx(string lpFileName, int flags, out WIN32_FILE_ATTRIBUTE_DATA fileData);

        [StructLayout(LayoutKind.Sequential)]
        private struct WIN32_FILE_ATTRIBUTE_DATA
        {
            public FileAttributes dwFileAttributes;
            public long ftCreationTime;
            public long ftLastAccessTime;
            public long ftLastWriteTime;
            public long nFileSize;
        }
        #endregion

        /// <summary>
        /// Delete the specified file.
        /// </summary>
        /// <param name="filename">Full name of file to delete.</param>
        /// <returns>True if successfully deleted</returns>
        /// <remarks>
        /// Does not throw exceptions.
        /// </remarks>
        public static bool Delete(string filename) => DeleteFile(filename);

        /// <summary>
        ///  Copy a file to a new filename.
        /// </summary>
        /// <param name="srcfile">File name of source file</param>
        /// <param name="dstFile">File name of destination file</param>
        /// <param name="failIfExists"></param>
        /// <returns>True if successful</returns>
        /// <remarks>
        /// Does not throw exceptions.
        /// </remarks>
        public static bool Copy(string srcfile, string dstFile, bool failIfExists = false) => CopyFile(srcfile, dstFile, failIfExists);

        /// <summary>
        /// Move a file to a new destination.
        /// </summary>
        /// <param name="srcfile">File name of source file</param>
        /// <param name="dstFile">File name of destination file</param>
        /// <returns>True if successful</returns>
        /// <remarks>
        /// Does not throw exceptions.
        /// A pre-existing destination file is overwritten.
        /// May move files across drives.
        /// </remarks>
        public static bool Move(string srcfile, string dstFile) => MoveFileEx(srcfile, dstFile, 3);

        /// <summary>
        /// Get length of specified file 
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>File length or -1 upon error.</returns>
        /// <remarks>
        /// Does not throw exceptions.
        /// </remarks>
        public static long Length(string filename)
        {
            bool success = GetFileAttributesEx(filename, 0, out WIN32_FILE_ATTRIBUTE_DATA fileData);
            if (!success) return -1L;
            return fileData.nFileSize;
        }

        /// <summary>
        /// Check if a file exists.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>True if file exists.</returns>
        /// <remarks>
        /// Does not throw exceptions.
        /// </remarks>
        public static bool Exists(string filename) => GetFileAttributes(filename) != -1;

        /// <summary>
        /// Get all 3 datetime fields for a given file in FileTime (64-bit) format.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="creationTime"></param>
        /// <param name="lastAccessTime"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns>True if successful</returns>
        public static bool GetFileTime(string filename, out long creationTime, out long lastAccessTime, out long lastWriteTime)
        {
            creationTime = lastAccessTime = lastWriteTime = 0;

            //bool success = GetFileAttributesEx(filename, 0, out WIN32_FILE_ATTRIBUTE_DATA fileData);
            //if (!success) return false;
            //creationTime = fileData.ftCreationTime;
            //lastAccessTime = fileData.ftLastAccessTime;
            //lastWriteTime = fileData.ftLastWriteTime;

            var hFile = CreateFile(filename, 0x0080, 0x00000003, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
            if (hFile == INVALID_HANDLE_VALUE) return false;
            bool success = GetFileTime(hFile, out creationTime, out lastAccessTime, out lastWriteTime);
            CloseHandle(hFile);
            return success;
        }

        /// <summary>
        /// Set datetime fields for a given file in FileTime (64-bit) format. Time field value 0 == not modified.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="creationTime"></param>
        /// <param name="lastAccessTime"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns>True if successful</returns>
        public static bool SetFileTime(string filename, long creationTime, long lastAccessTime, long lastWriteTime)
        {
            bool success;
            var hFile = CreateFile(filename, 0x0100, 0x00000003, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
            if (hFile == INVALID_HANDLE_VALUE) return false;

            var fields = (creationTime == 0 ? 0 : 1) | (lastAccessTime == 0 ? 0 : 2) | (lastWriteTime == 0 ? 0 : 4);

            switch (fields)
            {
                case 0x01: success = SetFileTime(hFile, ref creationTime, IntPtr.Zero, IntPtr.Zero); break;
                case 0x02: success = SetFileTime(hFile, IntPtr.Zero, ref lastAccessTime, IntPtr.Zero); break;
                case 0x03: success = SetFileTime(hFile, ref creationTime, ref lastAccessTime, IntPtr.Zero); break;
                case 0x04: success = SetFileTime(hFile, IntPtr.Zero, IntPtr.Zero, ref lastWriteTime); break;
                case 0x05: success = SetFileTime(hFile, ref creationTime, IntPtr.Zero, ref lastWriteTime); break;
                case 0x06: success = SetFileTime(hFile, IntPtr.Zero, ref lastAccessTime, ref lastWriteTime); break;
                case 0x07: success = SetFileTime(hFile, ref creationTime, ref lastAccessTime, ref lastWriteTime); break;
                default: success = false; break;
            }

            CloseHandle(hFile);
            return success;
        }

        public static void SetFileDateTime(string filename, DateTime dt)
        {
            var filetime = dt.ToFileTime();
            FileEx.SetFileTime(filename, filetime, filetime, filetime);
        }

        public static string GetDefaultExtension(string mimeType, string defalt)
        {
            if (string.IsNullOrEmpty(mimeType)) return defalt;
            mimeType = mimeType.Split(';')[0].Trim(); //"text/html; charset=UTF-8"
            string ext = null;
            try { ext = Registry.GetValue(@"HKEY_CLASSES_ROOT\MIME\Database\Content Type\" + mimeType, "Extension", string.Empty)?.ToString(); }
            catch { }
            if (string.IsNullOrEmpty(ext)) return defalt; //If all else fails, we assume the caller is correct.

            if (ext == ".html") ext = ".htm";  //Override registry mimetypes. We like the legacy extensions.
            if (ext == ".jfif") ext = ".jpg";

            return ext;
        }

        private static string GetFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }

        private static readonly Object GetUniqueFilename_Lock = new Object();  //used exclusively by GetUniqueFilename()
        public static string GetUniqueFilename(string srcFilename)
        {
            // Securely find an unused filename in a multi-threaded environment.

            srcFilename = GetFullPath(srcFilename);
            if (string.IsNullOrEmpty(srcFilename)) return null;

            string pathFormat = null;
            string newFilename = srcFilename;
            int index = 1;

            lock (GetUniqueFilename_Lock)
            {
                string dir = Path.GetDirectoryName(srcFilename);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                while (File.Exists(newFilename))
                {
                    if (pathFormat == null)
                    {
                        string path = Path.Combine(dir, Path.GetFileNameWithoutExtension(srcFilename));
                        if (path[path.Length - 1] == ')')
                        {
                            int i = path.LastIndexOf('(');
                            if (i > 0) path = path.Substring(0, i);
                        }
                        pathFormat = path + "({0:00})" + Path.GetExtension(srcFilename);
                    }
                    newFilename = string.Format(pathFormat, index++);
                }

                File.Create(newFilename).Dispose();  //create place-holder file.
            }

            return newFilename;
        }

        public static string ValidateUri(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
            try
            {
                var uri = new Uri(url);
                return uri.AbsoluteUri;
            }
            catch
            {
                return null;
            }
        }

    }
}
