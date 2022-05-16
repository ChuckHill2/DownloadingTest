//--------------------------------------------------------------------------
// <summary>
//   
// </summary>
// <copyright file="WebDownloader.cs" company="Chuck Hill">
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DownloadingTest
{
    /// <summary>
    /// Data downloader. This is not thread-safe. Use once and dispose.
    /// Exception: NotSupportedException:WebClient does not support concurrent I/O operations.
    /// </summary>
    public class WebDownloader : IDisposable
    {
        //private const string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:96.0) Gecko/20100101 Firefox/96.0";
        private const string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.88 Safari/537.36";

        private MyWebClient Client;

        /// <summary>
        /// Enable logging by assigning a logging writer function.  This may be a performance hit. Setting it back to null effectively disables logging.
        /// </summary>
        public Action<Severity, string> LogWriter { get; set; }
        private void LogWrite(Severity severity, string fmt, params object[] args)
        {
            if (LogWriter == null) return;
            if (args != null && args.Length > 0)
                fmt = string.Format(fmt, args);
            LogWriter(severity, fmt);
        }

        public WebDownloader()
        {
            Client = new MyWebClient();
        }

        public void Dispose()
        {
            if (Client == null) return;
            Client.Dispose();
            Client = null;
        }

        public async Task<bool> DownloadAsync(Job job)
        {
            bool success = false;

            for (int retry = 0; retry <= 0; retry++) //maybe retry for redirects.
            {
                success = false;
                SetJobException(job, null);
                try
                {
                    await DownloadThrow(job);
                    return true;
                }
                catch (Exception ex)
                {
                    SetJobException(job, ex);
                    IncrementJobFailureCount(job);

                    #region Handle Disk-full error
                    const int ERROR_HANDLE_DISK_FULL = 0x27;
                    const int ERROR_DISK_FULL = 0x70;
                    int hResult = ex.HResult & 0xFFFF;

                    if (hResult == ERROR_HANDLE_DISK_FULL || hResult == ERROR_DISK_FULL) //There is not enough space on the disk.
                    {
                        LogWrite(Severity.Error, "<<<<<<< Disk Full >>>>>>>\r\n{0}", ex);
                        throw ex;  //very bad
                    }
                    #endregion Handle Disk-full error

                    if (job.Filename != null) FileEx.Delete(job.Filename);

                    HttpStatusCode httpStatus = 0;
                    WebExceptionStatus webStatus = WebExceptionStatus.Success;
                    if (ex is WebException)
                    {
                        WebException we = (WebException)ex;
                        HttpWebResponse response = we.Response as System.Net.HttpWebResponse;
                        httpStatus = response?.StatusCode ?? 0;
                        webStatus = we.Status;
                    }

                    if (webStatus == WebExceptionStatus.TrustFailure)
                    {
                        if (job.FailureCount <= 1)
                        {
                            var newurl = job.Url;
                            if (newurl.StartsWith("https://")) newurl = newurl.Replace("https://", "http://");
                            else if (newurl.StartsWith("http://")) newurl = newurl.Replace("http://", "https://");
                            LogWrite(Severity.Warning, "{0}{1}: Retry {2} ==> {3}", JobNumberMsg(job), webStatus, newurl, job.Url);
                            SetJobUrl(job, newurl);
                            retry--;
                            continue;
                        }
                    }

                    //Occurs when the web server is flooded with our requests due to multi-threading.
                    if (webStatus == WebExceptionStatus.RequestCanceled
                     || webStatus == WebExceptionStatus.Timeout
                     || webStatus == WebExceptionStatus.UnknownError)
                    {
                        if (job.FailureCount <= 1)
                        {
                            LogWrite(Severity.Warning, "{0}{1}: Retry {2}", JobNumberMsg(job), DownloadStatus(webStatus, httpStatus), Url2FilenameMsg(job));
                            Thread.Sleep(2000);
                            retry--;
                            continue;
                        }
                    }

                    #if DEBUG
                        LogWrite(Severity.Error, "{0}{1} {2}\r\n{3}", JobNumberMsg(job), DownloadStatus(webStatus, httpStatus), ex, Url2FilenameMsg(job));
                    #else
                        LogWrite(Severity.Error, "{0}{1}{2}:{3} : {4}", JobNumberMsg(job), DownloadStatus(webStatus, httpStatus), ex.GetType().Name, ex.Message, Url2FilenameMsg(job));
                    #endif
                }
            }

            return success;
        }

        private async Task DownloadThrow(Job job)
        {
            Stream stream = null;
            Client.Headers[HttpRequestHeader.UserAgent] = UserAgent;
            if (!string.IsNullOrEmpty(job.Referer)) Client.Headers[HttpRequestHeader.Referer] = job.Referer;
            if (!string.IsNullOrEmpty(job.Cookie)) Client.Headers[HttpRequestHeader.Cookie] = job.Cookie;
            Client.Headers[HttpRequestHeader.AcceptLanguage] = "en-US,en;q=0.5"; //always set language to english so our web scraper only has to deal with only a single language.
            job.Filename = FileEx.GetUniqueFilename(job.Filename); //creates empty file as placeholder

            if (job.Filename != null)
                await Client.DownloadFileTaskAsync(job.Url, job.Filename); //This will throw an exception if the url cannot be downloaded.
            else
                stream = await Client.OpenReadTaskAsync(job.Url);

            SetJobUrl(job, Client.ResponseUrl??job.Url); //update url to what the Web server thinks it is.
            string cookie = Client.ResponseHeaders[HttpResponseHeader.SetCookie];
            if (!string.IsNullOrEmpty(cookie)) job.Cookie = cookie;
            SetJobLastModified(job,
                !DateTime.TryParse(Client.ResponseHeaders[HttpResponseHeader.LastModified] ?? string.Empty, out var lastModified) ? 
                !DateTime.TryParse(Client.ResponseHeaders[HttpResponseHeader.Date] ?? string.Empty, out lastModified) ? 
                DateTime.Now : lastModified : lastModified);

            SetJobMimeType(job, GetMimeType(Client, out var charset));
            int contentLength = int.TryParse(Client.ResponseHeaders[HttpResponseHeader.ContentLength], out var __contentLength) ? __contentLength : 0;

            if (job.Filename == null)
            {
                if (charset == null) //string field cannot have binary data
                {
                    if (contentLength==0 || contentLength > 1024 * 1024) //Any larger and we may run out of memory!
                        job.Body = $"{job.MimeType ?? "UNKNOWN"} binary data in excess of 1Mb is not stored in this object. Try again with a provided file destination.";
                    else
                    {
                        var ms = new MemoryStream(contentLength);
                        await stream.CopyToAsync(ms);
                        ms.Position = 0;
                        job.Body = System.Convert.ToBase64String(ms.ToArray(), Base64FormattingOptions.InsertLineBreaks);
                        stream.Dispose();
                        ms.Dispose();
                        //use: var bytes = Convert.FromBase64String(job.Body); to decode.
                    }
                }
                else
                {
                    using (StreamReader reader = new StreamReader(stream, Encoding.GetEncoding(charset)))
                    {
                        job.Body = await reader.ReadToEndAsync();
                    }
                }
            }
            else
            {
                if (!FileEx.Exists(job.Filename)) throw new FileNotFoundException("");  //should never occur due to GetUniqueFilename()
                if (FileEx.Length(job.Filename) < 8) { FileEx.Delete(job.Filename); throw new FileNotFoundException("No Data."); }

                //Adjust extension to reflect true filetype, BUT make sure that new filename does not exist.

                var oldExt = Path.GetExtension(job.Filename);
                var newExt = FileEx.GetDefaultExtension(job.MimeType, oldExt);
                if (!oldExt.Equals(newExt, StringComparison.OrdinalIgnoreCase))
                {
                    var newfilename = Path.ChangeExtension(job.Filename, newExt);
                    newfilename = FileEx.GetUniqueFilename(newfilename); //creates empty file as placeholder
                    FileEx.Delete(newfilename); //delete the placeholder. Move will throw exception if it already exists
                    FileEx.Move(job.Filename, newfilename);
                    job.Filename = newfilename; //return new filename to caller.
                }

                FileEx.SetFileDateTime(job.Filename, job.LastModified);
            }
        }

        private static string GetMimeType(WebClient client, out string charset)
        {
            charset = null;
            var contentType = client.ResponseHeaders[HttpResponseHeader.ContentType]; //"text/html; charset=UTF-8"
            if (string.IsNullOrEmpty(contentType)) return null;
            var items = contentType.Split(new char[] { ';', ' ', '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (items.Length > 2 && items[1].Equals("charset", StringComparison.OrdinalIgnoreCase)) charset = items[2];
            return items[0];
        }

        #region Private Job Setters
        // Only *We* are allowed to set these Job values.

        //private static readonly MethodInfo jobRetry = typeof(Job).GetProperty("FailureCount").GetSetMethod(true);
        private static void IncrementJobFailureCount(Job job) => job.FailureCount++; // jobRetry.Invoke(job, new object[] { job.FailureCount + 1 });

        //private static readonly MethodInfo jobUrl = typeof(Job).GetProperty("Url").GetSetMethod(true);
        private static void SetJobUrl(Job job, string url) => job.Url = url; // jobUrl.Invoke(job, new object[] { url });

        //private static readonly MethodInfo jobMimeType = typeof(Job).GetProperty("MimeType").GetSetMethod(true);
        private static void SetJobMimeType(Job job, string mimetype) => job.MimeType = mimetype; // jobMimeType.Invoke(job, new object[] { mimetype });

        //private static readonly MethodInfo jobLastModified = typeof(Job).GetProperty("LastModified").GetSetMethod(true);
        private static void SetJobLastModified(Job job, DateTime lastModified) => job.LastModified = lastModified; // jobLastModified.Invoke(job, new object[] { lastModified });

        //private static readonly MethodInfo jobException = typeof(Job).GetProperty("Exception").GetSetMethod(true);
        private static void SetJobException(Job job, Exception exception) => job.Exception = exception; // jobException.Invoke(job, new object[] { exception });
        #endregion

        #region Log.Write message formatted fragments
        private string JobNumberMsg(Job job)
        {
            if (LogWriter == null) return string.Empty;
            return job.JobNumber < 0 ? "" : job.JobNumber.ToString("0000 ");  // "0012 " or ""
        }
        private string Url2FilenameMsg(Job job)
        {
            if (LogWriter == null) return string.Empty;
            return job.Url + (job.Filename == null ? "" : " ==> " + Truncate(job.Filename)); // " ==> ...ster\mydir\myfile.htm" or ""
        }
        private string Truncate(string s)
        {
            if (LogWriter == null) return string.Empty;
            const int maxLen = 35; //does not include ellipsis prefix.
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Length <= maxLen) return s;
            return "\x2026" + s.Substring(s.Length - maxLen, maxLen);
        }

        private string DownloadStatus(WebExceptionStatus webStatus, HttpStatusCode httpStatus)
        {
            if (LogWriter == null) return string.Empty;
            var sb = new StringBuilder();
            if (webStatus != 0) sb.Append(webStatus);
            if (httpStatus != 0)
            {
                if (sb.Length > 0) sb.Append("/");
                var desc = (int)httpStatus == 308 ? "PermenentRedirect" : httpStatus.ToString();
                sb.Append($"{desc}({(int)httpStatus})");
            }

            return sb.ToString();
        }
        #endregion

        private class MyWebClient : WebClient
        {
            public WebRequest Request { get; private set; }
            public WebResponse Response { get; private set; }
            public string ResponseUrl => this.Response?.ResponseUri?.AbsoluteUri;
            public CookieContainer CookieManager { get; } = new CookieContainer();

            protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
            {
                Request = request;
                Response = base.GetWebResponse(request, result);
                return Response;
            }

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

                if (request != null) //for http and https only
                {
                    //request.SupportsCookieContainer = true; not settable but always true
                    if (request.CookieContainer == null) request.CookieContainer = CookieManager;
                    request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;  //Allow this API to decompress http output.
                    request.AllowAutoRedirect = true; //always true
                }

                return Request;
            }
        }
    }
}
