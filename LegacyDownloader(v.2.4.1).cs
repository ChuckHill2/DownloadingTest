//--------------------------------------------------------------------------
// <summary>
//   
// </summary>
// <copyright file="FileEx.cs" company="Chuck Hill">
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
// <repository>https://github.com/ChuckHill2/VideoLibrarian</repository>
// <author>Chuck Hill</author>
//--------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace DownloadingTest
{
    /// <summary>
    /// The exact downloader used in VideoLibrarian v2.4.1
    /// </summary>
    public static class LegacyDownloader //previously FileEx in VideoLibrarian v2.4.1
    {
        private static readonly Object GetUniqueFilename_Lock = new Object();  //used exclusively by FileEx.GetUniqueFilename()

        /// <summary>
        /// Make sure specified file does not exist. If it does, add or increment
        /// version. Then create an empty file placeholder so it won't get usurped
        /// by another thread calling this function. Versioned file format:
        /// d:\dir\name(00).ext where '00' is incremented until one is not found.
        /// </summary>
        /// <param name="srcFilename"></param>
        /// <returns></returns>
        private static string GetUniqueFilename(string srcFilename) //find an unused filename
        {
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

        /// <summary>
        /// Get file extension (with leading '.') from url.
        /// If none found, assumes ".htm"
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetUrlExtension(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                string ext = Path.GetExtension(uri.AbsolutePath).ToLower();
                if (string.IsNullOrEmpty(ext)) ext = ".htm";
                else if (ext == ".html") ext = ".htm";
                else if (ext == ".jpe") ext = ".jpg";
                else if (ext == ".jpeg") ext = ".jpg";
                else if (ext == ".jfif") ext = ".jpg";
                return ext;
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// Make absolute url from baseUrl + relativeUrl.
        /// If relativeUrl contains an absolute Url, returns that url unmodified.
        /// If any errors occur during combination of the two parts, string.Empty is returned.
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="relativeUrl"></param>
        /// <returns>absolute Url</returns>
        public static string GetAbsoluteUrl(string baseUrl, string relativeUrl)
        {
            try
            {
                return new Uri(new Uri(baseUrl), relativeUrl).AbsoluteUri;
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// Get earliest file or directory datetime.
        /// Empirically, it appears that the LastAccess or LastWrite times can be 
        /// earlier than the Creation time! For consistency, this method just 
        /// returns the earliest of these three file datetimes.
        /// </summary>
        /// <param name="filename">Full directory or filepath</param>
        /// <returns>DateTime</returns>
        public static DateTime GetCreationDate(string filename)
        {
            var dtMin = File.GetCreationTime(filename);

            var dt = File.GetLastAccessTime(filename);
            if (dt < dtMin) dtMin = dt;

            dt = File.GetLastWriteTime(filename);
            if (dt < dtMin) dtMin = dt;

            //Forget hi-precision and DateTimeKind. It just complicates comparisons. This is more than good enough.
            return new DateTime(dtMin.Year, dtMin.Month, dtMin.Day, dtMin.Hour, dtMin.Minute, 0);
        }

        /// <summary>
        /// Download a URL output into a local file.
        /// Will not throw an exception. Errors are written to Log.Write().
        /// </summary>
        /// <param name="data">Job to download (url and suggested destination filename, plus more)</param>
        /// <returns>True if successfully downloaded</returns>
        public static bool Download(Job data)
        {
            //This string is apparently not stored anywhere. It must be retrieved as a response from a web service via the browser of choice. It cannot be retrieved offline! Arrgh! Google: what is my useragent
            const string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:96.0) Gecko/20100101 Firefox/96.0";

            Uri uri = null;
            int t1 = Environment.TickCount;

            try
            {
                uri = new Uri(data.Url);

                string ext = Path.GetExtension(data.Filename);
                string mimetype = null;
                DateTime lastModified;

                //Fix for exception: The request was aborted: Could not create SSL/TLS secure channel
                //https://stackoverflow.com/questions/10822509/the-request-was-aborted-could-not-create-ssl-tls-secure-channel
                if (ServicePointManager.ServerCertificateValidationCallback==null)
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; }; //Skip validation of SSL/TLS certificate
                if (uri.Host.EndsWith("amazon.com", StringComparison.OrdinalIgnoreCase)) //HACK: Empirically required for httpś://m.media-amazon.com/images/ poster images.
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
                else
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;

                using (var web = new MyWebClient())
                {
                    web.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                    if (!string.IsNullOrEmpty(data.Referer)) web.Headers[HttpRequestHeader.Referer] = data.Referer;
                    if (!string.IsNullOrEmpty(data.Cookie)) web.Headers[HttpRequestHeader.Cookie] = data.Cookie;
                    web.Headers[HttpRequestHeader.AcceptLanguage] = "en-US,en;q=0.5"; //always set language to english so our web scraper only has to deal with only a single language.
                    data.Filename = FileEx.GetUniqueFilename(data.Filename); //creates empty file as placeholder

                    web.DownloadFile(data.Url, data.Filename);

                    data.Url = web.ResponseUrl; //update url to what the Web server thinks it is.
                    string cookie = web.ResponseHeaders[HttpResponseHeader.SetCookie];
                    if (!string.IsNullOrEmpty(cookie)) data.Cookie = cookie;
                    if (!DateTime.TryParse(web.ResponseHeaders[HttpResponseHeader.LastModified] ?? string.Empty, out lastModified)) lastModified = DateTime.Now;
                    mimetype = web.ResponseHeaders[HttpResponseHeader.ContentType];
                }
                if (!File.Exists(data.Filename)) throw new FileNotFoundException();

                if (new FileInfo(data.Filename).Length < 8) { File.Delete(data.Filename); throw new FileNotFoundException("File truncated."); }

                File.SetCreationTime(data.Filename, lastModified);
                File.SetLastAccessTime(data.Filename, lastModified);
                File.SetLastWriteTime(data.Filename, lastModified);

                //Adjust extension to reflect true filetype, BUT make sure that new filename does not exist.
                ext = GetDefaultExtension(mimetype, ext);
                if (!ext.Equals(Path.GetExtension(data.Filename),StringComparison.OrdinalIgnoreCase))
                {
                    var newfilename = Path.ChangeExtension(data.Filename, ext);
                    newfilename = FileEx.GetUniqueFilename(newfilename); //creates empty file as placeholder
                    File.Delete(newfilename); //delete the placeholder. Move will throw exception if it already exists
                    File.Move(data.Filename, newfilename);
                    data.Filename = newfilename; //return new filename to caller.
                }

                Log.Write(Severity.Verbose, $"Download {data.Url} duration={((Environment.TickCount - t1) / 1000f):F2} sec");
                return true;
            }
            catch (Exception ex)
            {
                File.Delete(data.Filename);

                HttpStatusCode responseStatus = (HttpStatusCode)0;
                WebExceptionStatus status = WebExceptionStatus.Success;
                if (ex is WebException)
                {
                    WebException we = (WebException)ex;
                    HttpWebResponse response = we.Response as System.Net.HttpWebResponse;
                    responseStatus = (response == null ? (HttpStatusCode)0 : response.StatusCode);
                    status = we.Status;
                }
                if (responseStatus == (HttpStatusCode)308) //The remote server returned an error: (308) Permanent Redirect.
                {
                    Log.Write(Severity.Error, $"duration={((Environment.TickCount - t1) / 1000f):F2} sec, {data.Url} ==> {Path.GetFileName(data.Filename)}: {ex.GetType().Name}:{ex.Message}\n       Shortcut may be corrupted!");
                }
                else
                {
                    Log.Write(Severity.Error, $"duration={((Environment.TickCount - t1) / 1000f):F2} sec, {data.Url} ==> {Path.GetFileName(data.Filename)}: {ex.GetType().Name}:{ex.Message}");
                }

                return false;
            }
        }

        private static string GetDefaultExtension(string mimeType, string defalt) //used exclusively by Download()
        {
            if (string.IsNullOrEmpty(mimeType)) return defalt;
            mimeType = mimeType.Split(';')[0].Trim();
            string ext;
            try { ext = Registry.GetValue(@"HKEY_CLASSES_ROOT\MIME\Database\Content Type\" + mimeType, "Extension", string.Empty).ToString(); }
            catch { ext = defalt; }

            if (ext == ".html") ext = ".htm";  //Override registry mimetypes. We like the legacy extensions.
            if (ext == ".jfif") ext = ".jpg";

            return ext;
        }

        private class MyWebClient : WebClient
        {
            public WebRequest Request { get; private set; }
            public WebResponse Response { get; private set; }
            public string ResponseUrl => this.Response?.ResponseUri?.AbsoluteUri; //gets the URI of the Internet resource that actually responded to the request.

            protected override WebResponse GetWebResponse(WebRequest request) //used internally
            {
                Request = request;
                Response = base.GetWebResponse(request);
                return Response;
            }

            protected override WebRequest GetWebRequest(Uri address) //used internally
            {
                Request = base.GetWebRequest(address);
                HttpWebRequest request = Request as HttpWebRequest; //there are others: e.g. FtpWebRequest (ftp://) and FileWebRequest (file://).
                
                if (request != null)
                {
                    request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;  //Allow this API to decompress http output.
                    request.AllowAutoRedirect = true;
                }

                return Request;
            }
        }
    }
}
