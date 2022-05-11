//--------------------------------------------------------------------------
// <summary>
//   
// </summary>
// <copyright file="HttpDownloader.cs" company="Chuck Hill">
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DownloadingTest
{
    /// <summary>
    /// Http Data downloader. This is thread-safe. It is safe to use as a singleton.
    /// </summary>
    public class HttpDownloader: IDisposable
    {
        //private const string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:96.0) Gecko/20100101 Firefox/96.0";
        private const string UserAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.88 Safari/537.36";

        private HttpClient Client;
        private HttpClientHandler ClientHandler;

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

        /// <summary>
        /// Gets access to the cookie manager. Well be empty if UseCookieManager is false.
        /// </summary>
        public CookieContainer CookieManager { get => ClientHandler.CookieContainer; }

        /// <summary>
        /// Gets the value that indicates whether that this instance uses the built-in cookie manager. May only be enabled/disabled via the constructor.
        /// </summary>
        public bool UseCookieManager { get => ClientHandler.UseCookies; }

        /// <summary>
        /// Downloader Constructor. May be used as a singleton.
        /// </summary>
        /// <param name="useCookieManager">
        /// True to use the built-in cookie manager. Default is true.<br/>
        /// If true, maintins the cookie state between multiple calls of the same url within this instance.<br/>
        /// If true, Job.Cookie will keep the cookie state for reuse in another instance.<br/>
        /// If false, each call to the same url refers to a new unique call. Useful to avoid the server from keeping track of you.<br/>
        /// </param>
        /// <remarks>
        /// https://github.com/dotnet/aspnetcore/issues/28385#issuecomment-853766480
        ///  http://byterot.blogspot.com/2016/07/singleton-httpclient-dns.html
        ///  Using this a a singleton for the life of the app is OK.
        ///  The only negative remark is the server may pull the rug out from under this client if the server changes DNS while this open. This is a non-issue for us.
        /// </remarks>
        public HttpDownloader(bool useCookieManager = true)
        {
            ClientHandler = new HttpClientHandler();
            ClientHandler.UseCookies = useCookieManager; //We handle cookies ourselves via Job.Cookie.
            ClientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            ClientHandler.AllowAutoRedirect = true; //default==true

            // Try to reuse because it could eat up all the available ports from continous creating/disposing.
            Client = new HttpClient(ClientHandler);

            Client.DefaultRequestHeaders.Add("accept", "*/*;q=0.8");
            Client.DefaultRequestHeaders.Add("accept-encoding", "gzip, deflate");
            Client.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.9");
            //Client.DefaultRequestHeaders.Add("cookie", "");
            //Client.DefaultRequestHeaders.Add("referer", "");
            Client.DefaultRequestHeaders.Add("user-agent", UserAgent);

            //Client.Timeout = new TimeSpan(0, 0, 0, 0, 100000); //default == 1min 40 sec or 100,000ms
        }

        /// <summary>
        /// Disposes this instance. However it can be used as a singleton for the life of this 
        /// application. Dispose this if you really don't intend on using this any more.
        /// </summary>
        public void Dispose()
        {
            //Doesn't really need to be disposed. See: http://byterot.blogspot.com/2016/07/singleton-httpclient-dns.html
            if (Client == null) return;
            Client.CancelPendingRequests();
            Client.Dispose();
            Client = null;
        }

        /// <summary>
        /// Download a URL and output into a local file. Will not retry, however the return status suggests which urls to 
        /// retry. In addition one can make one's own determination to retry with Job.Exception and Job.Retry counter.
        /// </summary>
        /// <param name="job">Job to download (url and suggested destination filename, plus more)</param>
        /// <returns>Download status: Success, Fail, Retry, or Fatal.</returns>
        /// <remarks>
        /// Thread-safe<br/>
        /// This method calls DownLoadThrowAsync and handles all it's exceptions and logs all non-success issues.<br/>
        /// Upon successful download:<br/>
        /// • File date is set to the object date on the server.<br/>
        /// • File extension is updated to reflect the server mime type.<br/>
        /// • If file already exists, file version is incremented. (e.g. fileame(ver).ext)<br/>
        /// • Job.Cookie is assigned if header cookie is non-empty.<br/>
        /// • Job.Filename is always updated to the new filename.<br/>
        /// • Job.Url is always updated to the actual url used by the server (redirect?)<br/>
        /// Upon failure, Job.Exception is set.<br/>
        /// Upon retry, Job.Retry is incremented and Job.Exception is set.<br/>
        /// </remarks>
        public async Task<bool> DownloadAsync(Job job)
        {
            bool success = false;

            for (int retry = 0; retry <= 0; retry++) //maybe retry for redirects.
            {
                success = false;
                SetJobException(job, null);

                try
                {
                    await DownloadThrowAsync(job).ConfigureAwait(false);
                    return true;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException is WebException) ex = (WebException)ex.InnerException;
                    WebExceptionStatus webStatus = (ex as WebException)?.Status ?? 0;
                    SetJobException(job, ex);
                    IncrementJobFailureCount(job);

                    #region Handle Disk-full error
                    const int ERROR_HANDLE_DISK_FULL = 0x27;
                    const int ERROR_DISK_FULL = 0x70;
                    int hResult = ex.HResult & 0xFFFF;

                    if (hResult == ERROR_HANDLE_DISK_FULL || hResult == ERROR_DISK_FULL) //There is not enough space on the disk.
                    {
                        LogWrite(Severity.Error, "<<<<<<< Disk Full >>>>>>>\r\n{0}", ex);
                        throw ex;  //very, very bad
                    }
                    #endregion Handle Disk-full error

                    if (ex is HttpResponseExceptionDL)
                    {
                        var ex2 = (HttpResponseExceptionDL)ex;
                        int rs = (int)ex2.Response.StatusCode;

                        if (ex2.Response.StatusCode == HttpStatusCode.NoContent)
                        {
                            var hasCookie = !string.IsNullOrEmpty(job.Cookie);
                            LogWrite(hasCookie ? Severity.Info : Severity.Error, "{0}{1}: {2} ==> {3}", JobNumberMsg(job), GetStatusString(ex2.Response.StatusCode), 
                                job.Url, (hasCookie?"Has cookie but no content":"Has no cookie and no content"));
                            success = hasCookie;
                            break;
                        }

                        if ((rs >= 300 && rs <= 399) || rs >= 500)
                        {
                            if (rs >= 300 && rs <= 399 && job.FailureCount <= 1)
                            {
                                retry--;
                                LogWrite(Severity.Warning, "{0}{1}: Retry {2}", JobNumberMsg(job), GetStatusString(ex2.Response.StatusCode), Url2FilenameMsg(job));
                                continue;
                            }
                            LogWrite(Severity.Error, "{0}{1}: {2}", JobNumberMsg(job), GetStatusString(ex2.Response.StatusCode), Url2FilenameMsg(job));
                            break;
                        }
                    }

                    if (webStatus == WebExceptionStatus.TrustFailure)
                    {
                        if (job.FailureCount <= 1)
                        {
                            var newurl = job.Url;
                            if (newurl.StartsWith("https://")) newurl = newurl.Replace("https://", "http://");
                            else if (newurl.StartsWith("http://")) newurl = newurl.Replace("http://", "https://");
                            LogWrite(Severity.Warning, "{0}{1}: Retry {2} ==> {3}", JobNumberMsg(job), GetStatusString(webStatus), newurl, job.Url);
                            SetJobUrl(job, newurl);
                            retry--;
                            continue;
                        }
                    }

                    // Occurs when the web server is flooded with our requests due to multi-threading.
                    // if (weStatus == WebExceptionStatus.RequestCanceled
                    //  || weStatus == WebExceptionStatus.Timeout
                    //  || weStatus == WebExceptionStatus.UnknownError)
                    // {
                    //     LogWrite(Severity.Warning, "{0}{1}: {2}", JobNumberMsg(job), GetStatusString(webStatus), Url2FilenameMsg(job));
                    //     break;
                    // }

                    if (ex is HttpResponseExceptionDL) //Exception message is already formatted.
                    {
                        LogWrite(Severity.Error, "{0}{1}: {2}", JobNumberMsg(job), nameof(HttpResponseExceptionDL), ex.Message);
                        break;
                    }

                    #if DEBUG
                        LogWrite(Severity.Error, "{0}{1}: {2}\r\n{3}", JobNumberMsg(job), GetStatusString(webStatus), ex, Url2FilenameMsg(job));
                    #else
                        LogWrite(Severity.Error, "{0}{1}: {2}:{3} : {4}", JobNumberMsg(job), GetStatusString(webStatus), ex.GetType().Name, ex.Message, Url2FilenameMsg(job));
                    #endif
                    break;
                }
            }

            return success;
        }

        /// <summary>
        /// Download data from url. Will throw exceptions. It is up to the caller to deal with them.
        /// No logging performed in this function. Only exceptions.
        /// </summary>
        /// <param name="job">Job to download (url and suggested destination filename, plus more)</param>
        private async Task DownloadThrowAsync(Job job)
        {
            //https://github.com/dotnet/aspnetcore/issues/28385#issuecomment-853766480
            //https://stackoverflow.com/questions/12373738/how-do-i-set-a-cookie-on-httpclients-httprequestmessage
            HttpRequestMessage request = null;
            HttpResponseMessage response = null;

            try
            {
                request = new HttpRequestMessage();
                request.Method = string.IsNullOrEmpty(job.Body) ? HttpMethod.Get : HttpMethod.Post;
                if (!string.IsNullOrEmpty(job.Body))
                {
                    request.Content = new StringContent(job.Body);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }

                request.RequestUri = new Uri(job.Url);
                if (!string.IsNullOrEmpty(job.Referer)) request.Headers.Add("referer", job.Referer);
                if (!string.IsNullOrEmpty(job.Cookie))
                {
                    if (UseCookieManager)
                        CookieManager.SetCookies(request.RequestUri, job.Cookie);
                    else
                        request.Headers.Add("cookie", job.Cookie);
                }

                response = await Client.SendAsync(request).ConfigureAwait(false);
                job.Cookie = GetCookie(response);

                if ((int)response.StatusCode >= 400)
                    throw new HttpResponseExceptionDL($"{GetStatusString(response.StatusCode)}: {Url2FilenameMsg(job)}", response);

                if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399) //maybe update the url?
                {
                    if (response.Headers.TryGetValues("refresh", out var _refreshX))  //Legacy 'refresh' header property is equalivant to HttpStatusCode.Redirect
                    {
                        //"0;url=httpz://example.com/there" ==seconds to wait before going to url
                        var urlX = _refreshX.FirstOrDefault()?.Split('=')?[1];
                        if (urlX != null)
                        {
                            if (urlX != job.Url)
                            {
                                var oldUrl = job.Url;
                                SetJobUrl(job, urlX);
                                throw new HttpResponseExceptionDL($"{GetStatusString(response.StatusCode)}: Refresh {oldUrl} ==> {job.Url}", response);
                            }
                            response.StatusCode = HttpStatusCode.Conflict;
                            throw new HttpResponseExceptionDL($"{GetStatusString(response.StatusCode)}: Refresh Recursion {job.Url}", response);
                        }
                    }

                    var url = response.Headers.Location?.AbsoluteUri;
                    if (url != null)
                    {
                        if (url != job.Url)
                        {
                            var oldUrl = job.Url;
                            SetJobUrl(job, url);
                            throw new HttpResponseExceptionDL($"{GetStatusString(response.StatusCode)}: Refresh {oldUrl} ==> {job.Url}", response);
                        }
                        response.StatusCode = HttpStatusCode.Conflict;
                        throw new HttpResponseExceptionDL($"{GetStatusString(response.StatusCode)}: Refresh Recursion {job.Url}", response);
                    }
                }

                if (response.Headers.TryGetValues("refresh", out var _refresh))  //Legacy 'refresh' header property is equalivant to HttpStatusCode.Redirect
                {
                    //"0;url=httpz://example.com/there" ==seconds to wait before going to url
                    var url = _refresh.FirstOrDefault()?.Split('=')?[1];
                    if (url != null)
                    {
                        if (url != job.Url)
                        {
                            var oldUrl = job.Url;
                            SetJobUrl(job, url);
                            throw new HttpResponseExceptionDL($"{GetStatusString(response.StatusCode)}: Refresh {oldUrl} ==> {job.Url}", response);
                        }
                        response.StatusCode = HttpStatusCode.Conflict;
                        throw new HttpResponseExceptionDL($"{GetStatusString(response.StatusCode)}: Refresh Recursion {job.Url}", response);
                    }
                }

                job.Body = null;
                SetJobUrl(job, response.RequestMessage.RequestUri.AbsoluteUri);
                SetJobLastModified(job, GetLastModified(response));
                var contentLength = response.Content.Headers.ContentLength.HasValue ? (int)response.Content.Headers.ContentLength.Value : 0;

                if (contentLength == 0 || response.StatusCode == HttpStatusCode.NoContent)
                {
                    response.StatusCode = HttpStatusCode.NoContent;
                    throw new HttpResponseExceptionDL($"{GetStatusString(response.StatusCode)}: No content recieved.", response);
                }

                SetJobMimeType(job, response.Content.Headers.ContentType?.MediaType);
                string charset = response.Content.Headers.ContentType?.CharSet; //e.g. "utf-8" or null for binary data
                job.Filename = GetFullPath(job.Filename);       //validate filename
                job.Filename = GetUniqueFilename(job.Filename); //creates empty file as placeholder
                if (job.Filename == null)
                {
                    if (charset==null) //string field cannot have binary data
                    {
                        if (contentLength > 1024*1024) //Any larger and we may run out of memory!
                            job.Body = $"{job.MimeType ?? "UNKNOWN"} binary data in excess of 1Mb is not stored in this object. Try again with a provided file destination.";
                        else
                        {
                            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                            job.Body = System.Convert.ToBase64String(bytes,Base64FormattingOptions.InsertLineBreaks);
                            //use: var bytes = Convert.FromBase64String(job.Body); to decode.
                        }
                    }
                    else
                    {
                        job.Body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    int bufSize; //try to optimize file stream buffer size.
                    if (contentLength == 0) bufSize = 80 * 1024; //Stream.CopyToAsync default
                    else if (contentLength <= 4096) bufSize = 4 * 1024;
                    else if (contentLength <= 128 * 1024) bufSize = contentLength;
                    else bufSize = 128 * 1024;

                    using (var fs = new FileStream(job.Filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, bufSize, useAsync: true))
                    {
                        //Warning: this may be a problem if the stream is text and something other than utf-8 encoding...
                        await response.Content.CopyToAsync(fs).ConfigureAwait(false);
                    }

                    //Adjust extension to reflect true filetype, BUT make sure that new filename does not exist.
                    var oldExt = Path.GetExtension(job.Filename);
                    var newExt = GetDefaultExtension(job.MimeType, oldExt);
                    if (!newExt.Equals(oldExt,StringComparison.InvariantCultureIgnoreCase))
                    {
                        var newfilename = Path.ChangeExtension(job.Filename, newExt);
                        newfilename = GetUniqueFilename(newfilename); //creates empty file as placeholder
                        FileEx.Delete(newfilename); //delete the placeholder. Move will throw exception if it already exists
                        FileEx.Move(job.Filename, newfilename);
                        job.Filename = newfilename; //return new filename to caller.
                    }

                    SetFileDate(job.Filename, job.LastModified);
                }
            }
            finally
            {
                response?.Dispose();
                request?.Dispose();
            }
        }

        #region Private Job Setters
        // Only *We* are allowed to set these Job values.

        private static readonly MethodInfo jobRetry = typeof(Job).GetProperty("FailureCount").GetSetMethod(true);
        private static void IncrementJobFailureCount(Job job) => jobRetry.Invoke(job, new object[] { job.FailureCount + 1 });

        private static readonly MethodInfo jobUrl = typeof(Job).GetProperty("Url").GetSetMethod(true);
        private static void SetJobUrl(Job job, string url) => jobUrl.Invoke(job, new object[] { url });

        private static readonly MethodInfo jobMimeType = typeof(Job).GetProperty("MimeType").GetSetMethod(true);
        private static void SetJobMimeType(Job job, string mimetype) => jobMimeType.Invoke(job, new object[] { mimetype });

        private static readonly MethodInfo jobLastModified = typeof(Job).GetProperty("LastModified").GetSetMethod(true);
        private static void SetJobLastModified(Job job, DateTime lastModified) => jobLastModified.Invoke(job, new object[] { lastModified });

        private static readonly MethodInfo jobException = typeof(Job).GetProperty("Exception").GetSetMethod(true);
        private static void SetJobException(Job job, Exception exception) => jobException.Invoke(job, new object[] { exception });
        #endregion

        private string GetCookie(HttpResponseMessage response)
        {
            if (UseCookieManager)
                return CookieManager.GetCookieHeader(response.RequestMessage.RequestUri);

            // The request header may only have ONE cookie header field but the response header may have multiple cookie header fields so we merge them here...
            // response.Headers.GetValues("set-Cookie")	{string[3]}	System.Collections.Generic.IEnumerable<string> {string[]}
            //     [0] "session-id=140-2096003-6020354; Domain=.imdb.com; Expires=Tue, 01-Jan-2036 08:00:01 GMT; Path=/"
            //     [1] "session-id-time=2082787201l; Domain=.imdb.com; Expires=Tue, 01-Jan-2036 08:00:01 GMT; Path=/"
            //     [2] "next-sid=DzX_99A0RtN3udMf29YQZ; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT; HttpOnly"

            if (!response.Headers.TryGetValues("set-cookie", out var cookies)) return null;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); //used to eliminate duplicates
            foreach(var cookie in cookies)
            {
                foreach(var crumb in cookie.Split(';'))
                {
                    var kv = crumb.Trim().Split('=');
                    if ("|Domain|Expires|Path|HttpOnly|".IndexOf(kv[0], 0, StringComparison.OrdinalIgnoreCase) != -1) continue;
                    dict[kv[0]] = kv.Length > 1 ? kv[1]??"" : "";
                }
            }

            return string.Join("; ", dict.OrderBy(x=>x.Key).Select(x=>string.Concat(x.Key,"=",x.Value)));
        }

        private static DateTime GetLastModified(HttpResponseMessage response)
        {
            // Last-Modified: Tue, 15 Oct 2019 12:45:26 GMT
            // try to get server resource last modified date.
            // if that doesn't work, get the date the data was retrieved on the server-side.
            // if that doesn't work just get the now date.

            return response.Content.Headers.LastModified.HasValue ? response.Content.Headers.LastModified.Value.DateTime.ToLocalTime() :
                   response.Headers.Date.HasValue ? response.Headers.Date.Value.DateTime.ToLocalTime() : 
                   DateTime.Now;
        }

        private static string GetDefaultExtension(string mimeType, string defalt)
        { 
            if (string.IsNullOrEmpty(mimeType)) return defalt;
            mimeType = mimeType.Split(';')[0].Trim(); //"text/html; charset=UTF-8"
            string ext = null;
            try { ext = Registry.GetValue(@"HKEY_CLASSES_ROOT\MIME\Database\Content Type\" + mimeType, "Extension", string.Empty)?.ToString(); }
            catch {}
            if (string.IsNullOrEmpty(ext)) return defalt; //If all else fails, we assume the caller is correct.

            if (ext == ".html") ext = ".htm";  //Override registry mimetypes. We like the legacy extensions.
            if (ext == ".jfif") ext = ".jpg";

            return ext;
        }

        private static readonly Object GetUniqueFilename_Lock = new Object();  //used exclusively by GetUniqueFilename()
        private static string GetUniqueFilename(string srcFilename)
        {
            // Securely find an unused filename in a multi-threaded environment.

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

        private static void SetFileDate(string filename, DateTime dt)
        {
            var filetime = dt.ToFileTime();
            FileEx.SetFileTime(filename, filetime, filetime, filetime);
        }

        /// <summary>
        /// Return valid full path name or null if invalid. File does not need to exist.
        /// </summary>
        /// <param name="path">path name to test</param>
        /// <returns>full path name or null if invalid</returns>
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

        private static readonly Dictionary<int, string> __httpStatusCodes = InitHttpStatusCodes(); //used exclusively by GetStatusString()
        private string GetStatusString(WebExceptionStatus code) => GetStatusString((int)code);
        private string GetStatusString(HttpStatusCode code) => GetStatusString((int)code);
        private string GetStatusString(int code)
        {
            if (LogWriter == null) return string.Empty;
            if (code == 0) return string.Empty; //We don't show a status string for 'success'
            //WebExceptionStatus numbers and HttpStatusCode number do not overlap so we take advantage of that here.
            string s = string.Empty;
            if (code < 100)
            {
                s = ((WebExceptionStatus)code).ToString();
                var intStr = code.ToString();
                if (s == intStr) s = "WebExceptionStatus";
                return $"{s}({intStr})";
            }

            s = __httpStatusCodes.TryGetValue(code, out var result) ? result : "HttpStatusCode";
            return $"{s}({code})";
        }

        private static Dictionary<int, string> InitHttpStatusCodes()
        {
            //Not all http status codes are enumerated in System.Net.HttpStatusCode, so we have to put them here. Used for error messaging.
            //Created from https://en.wikipedia.org/wiki/List_of_HTTP_status_codes with a little regex parsing.
            return new Dictionary<int, string>()
            {
                //1xx informational response
                { 100, "Continue" },  //The server has received the request headers and the client should proceed to send the request body
                { 101, "SwitchingProtocols" },  //The requester has asked the server to switch protocols and the server has agreed to do so.
                { 102, "Processing" },  //A WebDAV request may contain many sub-requests involving file operations, requiring a long time to complete the request. This code indicates that the server has received and is processing the request, but no response is available yet. This prevents the client from timing out and assuming the request was lost.
                { 103, "EarlyHints" },  //Used to return some response headers before final HTTP message.
                //2xx success
                { 200, "OK" },  //Standard response for successful HTTP requests. The actual response will depend on the request method used. In a GET request, the response will contain an entity corresponding to the requested resource. In a POST request, the response will contain an entity describing or containing the result of the action.
                { 201, "Created" },  //The request has been fulfilled, resulting in the creation of a new resource.
                { 202, "Accepted" },  //The request has been accepted for processing, but the processing has not been completed. The request might or might not be eventually acted upon, and may be disallowed when processing occurs.
                { 203, "NonAuthoritativeInformation" },  //The server is a transforming proxy
                { 204, "NoContent" },  //The server successfully processed the request, and is not returning any content.
                { 205, "ResetContent" },  //The server successfully processed the request, asks that the requester reset its document view, and is not returning any content.
                { 206, "PartialContent" },  //The server is delivering only part of the resource
                { 207, "MultiStatus" },  //The message body that follows is by default an XML message and can contain a number of separate response codes, depending on how many sub-requests were made.
                { 208, "AlreadyReported" },  //The members of a DAV binding have already been enumerated in a preceding part of the
                { 226, "IMUsed" },  //The server has fulfilled a request for the resource, and the response is a representation of the result of one or more instance-manipulations applied to the current instance.
                //3xx redirection
                { 300, "Ambiguous" },  //aka 'MultipleChoices'. Indicates multiple options for the resource from which the client may choose
                { 301, "MovedPermanently" },  //This and all future requests should be directed to the given URI. 
                { 302, "Redirect" },  //aka 'Found'. Tells the client to look at (browse to) another URL.. RFC specification required the client to perform a temporary redirect with the same method (the original describing phrase was "Moved Temporarily"), but popular browsers implemented 302 redirects by changing the method to GET. Therefore, HTTP/1.1 added status codes 303 and 307 to distinguish between the two behaviours.
                { 303, "SeeOther" },  //aka 'RedirectMethod'. The response to the request can be found under another URI using the GET method. When received in response to a POST
                { 304, "NotModified" },  //Indicates that the resource has not been modified since the version specified by the request headers If-Modified-Since or If-None-Match. In such case, there is no need to retransmit the resource since the client still has a previously-downloaded copy.
                { 305, "UseProxy" },  //The requested resource is available only through a proxy, the address for which is provided in the response. For security reasons, many HTTP clients
                { 306, "SwitchProxy" },  //aka 'Unused'. No longer used. Originally meant "Subsequent requests should use the specified proxy."
                { 307, "TemporaryRedirect" },  //aka 'RedirectKeepVerb'. In this case, the request should be repeated with another URI; however, future requests should still use the original URI. In contrast to how 302 was historically implemented, the request method is not allowed to be changed when reissuing the original request. For example, a POST request should be repeated using another POST request.
                { 308, "PermanentRedirect" },  //This and all future requests should be directed to the given URI. 308 parallel the behaviour of 301, but does not allow the HTTP method to change. So, for example, submitting a form to a permanently redirected resource may continue smoothly.
                //4xx client errors
                { 400, "BadRequest" },  //The server cannot or will not process the request due to an apparent client error
                { 401, "Unauthorized" },  //Similar to 403 Forbidden, but specifically for use when authentication is required and has failed or has not yet been provided. The response must include a WWW-Authenticate header field containing a challenge applicable to the requested resource. See Basic access authentication and Digest access authentication. 401 semantically means "unauthorised", the user does not have valid authentication credentials for the target resource.
                { 402, "PaymentRequired" },  //Reserved for future use. The original intention was that this code might be used as part of some form of digital cash or micropayment scheme, as proposed, for example, by GNU Taler, but that has not yet happened, and this code is not widely used. Google Developers API uses this status if a particular developer has exceeded the daily limit on requests. Sipgate uses this code if an account does not have sufficient funds to start a call. Shopify uses this code when the store has not paid their fees and is temporarily disabled. Stripe uses this code for failed payments where parameters were correct, for example blocked fraudulent payments.
                { 403, "Forbidden" },  //The request contained valid data and was understood by the server, but the server is refusing action. This may be due to the user not having the necessary permissions for a resource or needing an account of some sort, or attempting a prohibited action
                { 404, "NotFound" },  //The requested resource could not be found but may be available in the future. Subsequent requests by the client are permissible.
                { 405, "MethodNotAllowed" },  //A request method is not supported for the requested resource; for example, a GET request on a form that requires data to be presented via POST, or a PUT request on a read-only resource.
                { 406, "NotAcceptable" },  //The requested resource is capable of generating only content not acceptable according to the Accept headers sent in the request. See Content negotiation.
                { 407, "ProxyAuthenticationRequired" },  //The client must first authenticate itself with the proxy.
                { 408, "RequestTimeout" },  //The server timed out waiting for the request. According to HTTP specifications: "The client did not produce a request within the time that the server was prepared to wait. The client MAY repeat the request without modifications at any later time."
                { 409, "Conflict" },  //Indicates that the request could not be processed because of conflict in the current state of the resource, such as an edit conflict between multiple simultaneous updates.
                { 410, "Gone" },  //Indicates that the resource requested is no longer available and will not be available again. This should be used when a resource has been intentionally removed and the resource should be purged. Upon receiving a 410 status code, the client should not request the resource in the future. Clients such as search engines should remove the resource from their indices. Most use cases do not require clients and search engines to purge the resource, and a "404 Not Found" may be used instead.
                { 411, "LengthRequired" },  //The request did not specify the length of its content, which is required by the requested resource.
                { 412, "PreconditionFailed" },  //The server does not meet one of the preconditions that the requester put on the request header fields.
                { 413, "RequestEntityTooLarge" },  //aka 'PayloadTooLarge'. The request is larger than the server is willing or able to process. Previously called "Request Entity Too Large".
                { 414, "RequestUriTooLong" },  //aka 'UriTooLong'. The URI provided was too long for the server to process. Often the result of too much data being encoded as a query-string of a GET request, in which case it should be converted to a POST request. Called "Request-URI Too Long" previously.
                { 415, "UnsupportedMediaType" },  //The request entity has a media type which the server or resource does not support. For example, the client uploads an image as image/svg+xml, but the server requires that images use a different format.
                { 416, "RequestedRangeNotSatisfiable" },  //aka 'RangeNotSatisfiable'. The client has asked for a portion of the file"Requested Range Not Satisfiable" previously.
                { 417, "ExpectationFailed" },  //The server cannot meet the requirements of the Expect request-header field.
                { 418, "I'm a teapot" },  //This code was defined in 1998 as one of the traditional IETF April Fools' jokes, in RFC 2324, Hyper Text Coffee Pot Control Protocol, and is not expected to be implemented by actual HTTP servers. The RFC specifies this code should be returned by teapots requested to brew coffee. This HTTP status is used as an Easter egg in some websites, such as Google.com's I'm a teapot easter egg.
                { 421, "MisdirectedRequest" },  //The request was directed at a server that is not able to produce a response
                { 422, "UnprocessableEntity" },  //The request was well-formed but was unable to be followed due to semantic errors.
                { 423, "Locked" },  //The resource that is being accessed is locked.
                { 424, "FailedDependency" },  //The request failed because it depended on another request and that request failed
                { 425, "TooEarly" },  //Indicates that the server is unwilling to risk processing a request that might be replayed.
                { 426, "UpgradeRequired" },  //The client should switch to a different protocol such as TLS/1.3, given in the Upgrade header field.
                { 428, "PreconditionRequired" },  //The origin server requires the request to be conditional. Intended to prevent the 'lost update' problem, where a client GETs a resource's state, modifies it, and PUTs it back to the server, when meanwhile a third party has modified the state on the server, leading to a conflict.
                { 429, "TooManyRequests" },  //The user has sent too many requests in a given amount of time. Intended for use with rate-limiting schemes.
                { 431, "RequestHeaderFieldsTooLarge" },  //The server is unwilling to process the request because either an individual header field, or all the header fields collectively, are too large.
                { 451, "UnavailableForLegalReasons" },  //A server operator has received a legal demand to deny access to a resource or to a set of resources that includes the requested resource. The code 451 was chosen as a reference to the novel Fahrenheit 451
                //5xx server errors
                { 500, "InternalServerError" },  //A generic error message, given when an unexpected condition was encountered and no more specific message is suitable.
                { 501, "NotImplemented" },  //The server either does not recognize the request method, or it lacks the ability to fulfil the request. Usually this implies future availability
                { 502, "BadGateway" },  //The server was acting as a gateway or proxy and received an invalid response from the upstream server.
                { 503, "ServiceUnavailable" },  //The server cannot handle the request
                { 504, "GatewayTimeout" },  //The server was acting as a gateway or proxy and did not receive a timely response from the upstream server.
                { 505, "HttpVersionNotSupported" },  //The server does not support the HTTP protocol version used in the request.
                { 506, "VariantAlsoNegotiates" },  //Transparent content negotiation for the request results in a circular reference.
                { 507, "InsufficientStorage" },  //The server is unable to store the representation needed to complete the request.
                { 508, "LoopDetected" },  //The server detected an infinite loop while processing the request
                { 510, "NotExtended" },  //Further extensions to the request are required for the server to fulfil it.
                { 511, "NetworkAuthenticationRequired" },  //The client needs to authenticate to gain network access. Intended for use by intercepting proxies used to control access to the network"captive portals" used to require agreement to Terms of Service before granting full Internet access via a Wi-Fi hotspot).
                //Unofficial codes
                { 419, "PageExpired" },  //Used by the Laravel Framework when a CSRF Token is missing or expired.
                //{ 420, "Method Failure" },  //A deprecated response used by the Spring Framework when a method has failed.
                //{ 420, "Enhance Your Calm" },  //Returned by version 1 of the Twitter Search and Trends API when the client is being rate limited; versions 1.1 and later use the 429 Too Many Requests response code instead. The phrase "Enhance your calm" comes from the 1993 movie Demolition Man, and its association with this number is likely a reference to cannabis.[citation needed]
                { 430, "RequestHeaderFieldsTooLarge" },  //Used by Shopify, instead of the 429 Too Many Requests response code, when too many URLs are requested within a certain time frame.
                { 450, "BlockedByWindowsParentalControls" },  //The Microsoft extension code indicated when Windows Parental Controls are turned on and are blocking access to the requested webpage.
                { 498, "InvalidToken" },  //Returned by ArcGIS for Server. Code 498 indicates an expired or otherwise invalid token.
                //{ 499, "Token Required" },  //Returned by ArcGIS for Server. Code 499 indicates that a token is required but was not submitted.
                { 509, "BandwidthLimitExceeded" },  //The server has exceeded the bandwidth specified by the server administrator; this is often used by shared hosting providers to limit the bandwidth of customers.
                { 529, "SiteIsOverloaded" },  //Used by Qualys in the SSLLabs server testing API to signal that the site can't process the request.
                { 530, "SiteIsFrozen" },  //Used by the Pantheon web platform to indicate a site that has been frozen due to inactivity.
                { 598, "NetworkReadTimeoutError" },  //Used by some HTTP proxies to signal a network read timeout behind the proxy to a client in front of the proxy.
                { 599, "NetworkConnectTimeoutError" },  //An error used by some HTTP proxies to signal a network connect timeout behind the proxy to a client in front of the proxy.
                //Microsoft IIS
                { 440, "LoginTimeout" },  //The client's session has expired and must log in again.
                { 449, "RetryWith" },  //The server cannot honour the request because the user has not provided the required information.
                //{ 451, "Redirect" },  //Used in Exchange ActiveSync when either a more efficient server is available or the server cannot access the users' mailbox. The client is expected to re-run the HTTP AutoDiscover operation to find a more appropriate server.
                //nginx
                { 444, "NoResponse" },  //Used internally to instruct the server to return no information to the client and close the connection immediately.
                { 494, "RequestHeaderTooLarge" },  //Client sent too large request or too long header line.
                { 495, "SslCertificateError" },  //An expansion of the 400 Bad Request response code, used when the client has provided an invalid client certificate.
                { 496, "SslCertificateRequired" },  //An expansion of the 400 Bad Request response code, used when a client certificate is required but not provided.
                { 497, "HttpRequestSentToHttpsPort" },  //An expansion of the 400 Bad Request response code, used when the client has made a HTTP request to a port listening for HTTPS requests.
                { 499, "ClientClosedRequest" },  //Used when the client has closed the request before the server could send a response.
                //Cloudflare
                { 520, "WebServerReturnedAnUnknownError" },  //The origin server returned an empty, unknown, or unexpected response to Cloudflare.
                { 521, "WebServerIsDown" },  //The origin server refused connections from Cloudflare. Security solutions at the origin may be blocking legitimate connections from certain Cloudflare IP addresses.
                { 522, "ConnectionTimedOut" },  //Cloudflare timed out contacting the origin server.
                { 523, "OriginIsUnreachable" },  //Cloudflare could not reach the origin server; for example, if the DNS records for the origin server are incorrect or missing.
                { 524, "ATimeoutOccurred" },  //Cloudflare was able to complete a TCP connection to the origin server, but did not receive a timely HTTP response.
                { 525, "SslHandshakeFailed" },  //Cloudflare could not negotiate a SSL/TLS handshake with the origin server.
                { 526, "InvalidSslCertificate" },  //Cloudflare could not validate the SSL certificate on the origin web server. Also used by Cloud Foundry's gorouter.
                { 527, "RailgunError" },  //Error 527 indicates an interrupted connection between Cloudflare and the origin server's Railgun server.
                //{ 530, "(CloudFlare)" },  //Error 530 is returned along with a 1xxx error.
                //AWS Elastic Load Balancer
                { 460, "AmazonAWS_460" },  //Client closed the connection with the load balancer before the idle timeout period elapsed. Typically when client timeout is sooner than the Elastic Load Balancer's timeout.
                { 463, "AmazonAWS_463" },  //The load balancer received an X-Forwarded-For request header with more than 30 IP addresses.
                { 561, "Unauthorized" },  //An error around authentication returned by a server registered with a load balancer. You configured a listener rule to authenticate users, but the identity provider
                //Caching warning codes
                { 110, "ResponseIsStale" },  //The response provided by a cache is stale
                { 111, "RevalidationFailed" },  //The cache was unable to validate the response, due to an inability to reach the origin server.
                { 112, "DisconnectedOperation" },  //The cache is intentionally disconnected from the rest of the network.
                { 113, "HeuristicExpiration" },  //The cache heuristically chose a freshness lifetime greater than 24 hours and the response's age is greater than 24 hours.
                { 199, "MiscellaneousWarning" },  //Arbitrary, non-specific warning. The warning text may be logged or presented to the user.
                { 214, "TransformationApplied" },  //Added by a proxy if it applies any transformation to the representation, such as changing the content encoding, media type or the like.
                { 299, "MiscellaneousPersistentWarning" },  //Same as 199, but indicating a persistent warning.
            };
        }
        #endregion

        #region Unused Code
#if UNUSED_CODE
        private static string[] InitUserAgentStrings()
        {
            //Downloaded from https://techblog.willshouse.com/2012/01/03/most-common-user-agents/
            //Last Updated: Mon, 18 Apr 2022 18:06:10 +0000

            return new string[]
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.75 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:98.0) Gecko/20100101 Firefox/98.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.82 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.84 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:99.0) Gecko/20100101 Firefox/99.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.88 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; rv:91.0) Gecko/20100101 Firefox/91.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.83 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.75 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.4 Safari/605.1.15",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.84 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64; rv:98.0) Gecko/20100101 Firefox/98.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:98.0) Gecko/20100101 Firefox/98.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.3 Safari/605.1.15",
                "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:98.0) Gecko/20100101 Firefox/98.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64; rv:99.0) Gecko/20100101 Firefox/99.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36 Edg/99.0.1150.46",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.88 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64; rv:91.0) Gecko/20100101 Firefox/91.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36 Edg/99.0.1150.55",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:99.0) Gecko/20100101 Firefox/99.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36 Edg/100.0.1185.29",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.127 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.82 Safari/537.36",
                "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:99.0) Gecko/20100101 Firefox/99.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.102 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.75 Safari/537.36 Edg/100.0.1185.36",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:97.0) Gecko/20100101 Firefox/97.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.109 Safari/537.36 OPR/84.0.4316.42",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.1 Safari/605.1.15",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:91.0) Gecko/20100101 Firefox/91.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.2 Safari/605.1.15",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.88 Safari/537.36",
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.84 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.84 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.83 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.84 Safari/537.36 OPR/85.0.4341.47",
                "Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.7113.93 Safari/537.36",
                "Mozilla/5.0 (X11; Fedora; Linux x86_64; rv:98.0) Gecko/20100101 Firefox/98.0",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.75 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.119 YaBrowser/22.3.0.2430 Yowser/2.5 Safari/537.36",
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.82 Safari/537.36",
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:98.0) Gecko/20100101 Firefox/98.0",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.75 Safari/537.36 Edg/100.0.1185.39",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.127 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.79 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36 Edg/99.0.1150.39",
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.109 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.2 Safari/605.1.15",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.102 Safari/537.36",
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:99.0) Gecko/20100101 Firefox/99.0",
                "Mozilla/5.0 (X11; Linux x86_64; rv:78.0) Gecko/20100101 Firefox/78.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.79 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.102 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Safari/605.1.15",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:96.0) Gecko/20100101 Firefox/96.0",
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.88 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.88 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:97.0) Gecko/20100101 Firefox/97.0",
                "Mozilla/5.0 (X11; Fedora; Linux x86_64; rv:93.0) Gecko/20100101 Firefox/93.0",
                "Mozilla/5.0 (X11; Linux x86_64; rv:97.0) Gecko/20100101 Firefox/97.0"
            };
        }
#endif
        #endregion Unused Code
    }

    /// <summary>
    /// Occurs when getting an unhandled HttpClient HttpStatusCode value. Functionally identical to 
    /// System.Web.Http.HttpResponseException (Microsoft.AspNetCore.Mvc.WebApiCompatShim) 
    /// except this allows for a custom message to be assigned.
    /// </summary>
    /// <remarks>
    /// https://github.com/aspnet/Mvc/blob/a6199bbfbab05583f987bae322fb04566841aaea/src/Microsoft.AspNetCore.Mvc.WebApiCompatShim/HttpResponseException.cs
    /// https://github.com/aspnet/AspNetWebStack/blob/main/src/System.Web.Http/HttpResponseException.cs
    /// </remarks>
    public class HttpResponseExceptionDL : Exception
    {
        public HttpResponseMessage Response { get; private set; }
        public HttpResponseExceptionDL(string message, HttpResponseMessage resp) : base(message, null) => Response = resp ?? throw new ArgumentNullException(nameof(resp));
    }
}
