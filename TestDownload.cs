//--------------------------------------------------------------------------
// <summary>
//   
// </summary>
// <copyright file="TestDownload.cs" company="Chuck Hill">
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DownloadingTest
{
    public enum DownloaderType { Web, Http, Sync }
    public enum DownloadTest { MiscUrls, ImdbUrls, SelectUrls }

    public static class TestDownload
    {
        private const string ImdbShortcutSourceFolder = @"C:\Users\User\source\repos\VideoLibrarian\TestVideos.Master.X";
        private static readonly string ProjectDir = GetProjectDir();
        private static readonly string ImdbUrlFile = ProjectDir + @"Resources\ImdbUrls.txt";
        private static readonly string MiscUrlFile = ProjectDir + @"Resources\MiscUrls.txt";
        private static readonly string TestFolder = GetTestFolder();
        private static readonly string[] CustomTestUrls = new string[]
        {
                "https://browserleaks.com/ip",      //View http request headers
                "http://answers.microsoft.com/",    //Redirect(302): continuous retries then fail --> due to url changing upon each retry when cookie manager disabled.
                "http://www.kaiserpermanente.org/", //BadRequest(400): Refresh Failure. success if CookieManager is true
                "https://10.6.198.39/",             //ConnectFailure: WebException:Unable to connect to the remote server
                "https://git.pandolar.top/",        //ConnectFailure: WebException:Unable to connect to the remote server
                "https://idm.xfinity.com/",         //Redirect(302): continuous retries then fail
                "https://support.avantree.com/",    //TrustFailure: WebException:The underlying connection was closed: Could not establish trust relationship for the SSL/TLS secure channel.  --> fixed
                "https://timeshareexitteam.com/",   //SendFailure: WebException:The underlying connection was closed: An unexpected error occurred on a send
                "https://secure3.i-doxs.net/",      //OK(200): No content recieved. (has cookies) --> fixed
                "https://wifilogin.xfinity.com/",   //OK(200): No content recieved.(no cookies or data) --> fixed
                "https://www.techwalla.com/",       //TaskCanceledException --> nothing to do. really Timeout: no response from server
                "http://payments.ebay.com/"         //NullReferenceException --> fixed
        };

        /// <summary>
        /// One shot test for a specified URL
        /// </summary>
        /// <param name="url">url to download</param>
        /// <param name="downloaderType">Which downloader to use.</param>
        /// <param name="useNullFilename">True to just retrieve data into Job.Body</param>
        public static void TestOne(string url, DownloaderType downloaderType, bool useNullFilename = false)
        {
            var job = useNullFilename ? new Job(url) : MakeJob(url);

            if (job.Filename != null) FileEx.Delete(job.Filename);
            HttpDownloader hdl = null;
            WebDownloader wdl = null;
            Func<Job, bool> download = null;

            try
            {
                if (downloaderType == DownloaderType.Web)
                {
                    wdl = new WebDownloader();
                    wdl.LogWriter = (s, f) => Log.Write(s, f);
                    download = j => wdl.DownloadAsync(j).Result;
                }
                else if (downloaderType == DownloaderType.Http)
                {
                    hdl = new HttpDownloader(true);
                    hdl.LogWriter = (s, f) => Log.Write(s, f);
                    download = j => hdl.DownloadAsync(j).Result;
                }
                else if (downloaderType == DownloaderType.Sync)
                {
                    Downloader.LogWriter = (s, f) => Log.Write(s, f);
                    download = j => Downloader.Download(j);
                }

                int t1 = Environment.TickCount;
                bool success = download(job);
                var duration = (Environment.TickCount - t1) / 1000f;

                Log.Write(Severity.Info, $"{job.Url} {(success ? "Successful" : "Failed")} {duration:F2} sec, {job.Url}");
            }
            catch (Exception ex)
            {
                Log.Write(Severity.Error, $"TestOne(\"{url}\", {downloaderType}Downloader, NullFile:{useNullFilename})\r\n{ex}");
            }
            finally
            {
                hdl?.Dispose();
                wdl?.Dispose();
            }
        }

        /// <summary>
        /// Download many urls simultaneously.
        /// </summary>
        /// <param name="test">Which set of built-in urls to use.</param>
        /// <param name="downloaderType">Which downloader to use.</param>
        /// <param name="maxDegreeOfParallelism">Maximum #threads to use for concurrent downloads. Use 1 for serial downloads.</param>
        /// <param name="urlCount">Download only a subset of built-in urls. Default is all.</param>
        /// <param name="doRetry">Retry download urls that fail hardccoded conditions.</param>
        public static void TestMany(DownloadTest test, DownloaderType downloaderType, int maxDegreeOfParallelism = int.MaxValue, int urlCount = int.MaxValue, bool doRetry = false)
        {
            IEnumerable<string> urls = null;

            if (test == DownloadTest.MiscUrls)
                urls = File.ReadAllLines(MiscUrlFile).Where(m => m.StartsWith("http")).Take(urlCount);
            if (test == DownloadTest.ImdbUrls)
                urls = File.ReadAllLines(ImdbUrlFile).Where(m => m.StartsWith("http")).Take(urlCount);
            if (test == DownloadTest.SelectUrls)
                urls = CustomTestUrls.Take(urlCount);

            Log.Write(Severity.None, $"******** {downloaderType} ********");

            TestMany(urls, downloaderType, maxDegreeOfParallelism, doRetry);

        }

        /// <summary>
        /// Download many urls simultaneously.
        /// </summary>
        /// <param name="urls">List of urls to download.</param>
        /// <param name="downloaderType">Which downloader to use.</param>
        /// <param name="maxDegreeOfParallelism">Maximum #threads to use for concurrent downloads. Use 1 for serial downloads.</param>
        /// <param name="doRetry">Retry download urls that fail builtin conditions.</param>
        public static void TestMany(IEnumerable<string> urls, DownloaderType downloaderType, int maxDegreeOfParallelism = int.MaxValue, bool doRetry = false)
        {
            List<Job> downloadJobs = null;
            Func<Job, bool> download = null;

            int jobNumber = 1; //job sequence order in order to match logging messages
            downloadJobs = urls.Select(url => MakeJob(url, jobNumber++)).ToList();
            List<float> durations = new List<float>(downloadJobs.Count);

            List<Job> redownloadJobs = new List<Job>(Math.Max(4, downloadJobs.Count / 2)); //for doRetry==true

            int Concurrency = 0;  //number of active threads.
            Console.WriteLine("Begin Downloading...");

            HttpDownloader hdl = null;
            WebDownloader wdl = null;
            try
            {
                if (downloaderType == DownloaderType.Web)
                {
                    wdl = new WebDownloader();
                    wdl.LogWriter = (s, f) => Log.Write(s, f); //enable logging
                    download = j => wdl.DownloadAsync(j).Result;
                }
                else if (downloaderType == DownloaderType.Http)
                {
                    hdl = new HttpDownloader();
                    hdl.LogWriter = (s, f) => Log.Write(s, f); //enable logging
                    download = j => hdl.DownloadAsync(j).Result;
                }
                else if (downloaderType == DownloaderType.Sync)
                {
                    Downloader.LogWriter = (s, f) => Log.Write(s, f); //enable logging
                    download = j => Downloader.Download(j);
                }

                //With a value larger than 100 and >500 IMDB jobs, it slows down the IMDB server so much that it times out with TaskCanceledException.
                var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };

                int tt = Environment.TickCount;

                while (downloadJobs.Count > 0)
                {
                    Parallel.ForEach(downloadJobs, options, job =>
                    {
                        var concurrency = Interlocked.Increment(ref Concurrency);
                        int t1 = Environment.TickCount;
                        var success = download(job);
                        var duration = (Environment.TickCount - t1) / 1000f;
                        lock (durations) durations.Add(duration);

                        // Built-in Conditions: Retry Detection Example
                        // if (!success && job.FailureCount <= 3)  //retry upon failure
                        // {
                        //    if (job.Exception is HttpResponseExceptionDL)  //only in HttpDownloader.DownloadAsync()
                        //    {
                        //        HttpStatusCode status = ((HttpResponseExceptionDL)job.Exception).Response.StatusCode;
                        //        if (status == HttpStatusCode.Continue)
                        //        {
                        //            lock (redownloadJobs) redownloadJobs.Add(job);
                        //        }
                        //    }
                        //    if (job.Exception is WebException webEx)
                        //    {
                        //        WebExceptionStatus status = webEx.Status;
                        //
                        //        //Occurs when the web server is flooded with our requests due to multi-threading.
                        //        if (status == WebExceptionStatus.RequestCanceled
                        //         || status == WebExceptionStatus.Timeout
                        //         || status == WebExceptionStatus.UnknownError)
                        //        {
                        //            lock (redownloadJobs) redownloadJobs.Add(job);
                        //        }
                        //    }
                        //    if (job.Exception is TaskCanceledException) //if IMDB.com, may be recoverable when running within Parallel.ForEach()
                        //    {
                        //        lock (redownloadJobs) redownloadJobs.Add(job);
                        //    }
                        // }

                        Log.Write(Severity.Info, $"{job.JobNumber:0000} {concurrency:0000} {(success ? "Successful" : "Failed")} {duration:F2} sec, {job.Url}");
                        Interlocked.Decrement(ref Concurrency);
                    });

                    if (!doRetry) break; //do not retry. We're done.

                    // Prepare for retry loop
                    downloadJobs.Clear();
                    var temp = downloadJobs;
                    downloadJobs = redownloadJobs;
                    redownloadJobs = temp;
                    if (downloadJobs.Count > 0) Log.Write(Severity.None, $"======== Retry ForEach({downloadJobs.Count}) ===========================================================================================");
                }

                var totalduration = (Environment.TickCount - tt) / 1000f;
                durations.Sort();
                Log.Write(Severity.Info, $"Summary " +
                    $"Count:{downloadJobs.Count} " +
                    $"Duration:{totalduration:F2} sec, " +
                    $"Sum:{durations.Sum():F2}, " +
                    $"Min:{durations[0]:F2}, " +
                    $"Max:{durations[downloadJobs.Count - 1]:F2}, " +
                    $"Range:{durations[downloadJobs.Count - 1] - durations[0]:F2}, " +
                    $"Ave:{durations.Average():F2}, " +
                    $"Median:{durations[downloadJobs.Count / 2]:F2}"); //not accurate but close enough.
            }
            catch (Exception ex)
            {
                Log.Write(Severity.Error, "Unexpected error occurred: " + ex);
                Console.WriteLine("Unexpected error occurred: "+ex.Message);
            }
            finally 
            {
                hdl?.Dispose();
                wdl?.Dispose();
            }

            Console.WriteLine($"Downloading Complete.");
        }

        #region Helpers
        // Regex pattern parse results:
        // https://github.com/search?l=C%23&q=base64+stream&type=Repositories ==> U: "github.com"
        // https://www.imdb.com/title/tt11947264 ==> T: "tt11947264"
        // https://www.imdb.com/title/tt11947264/ ==> T: "tt11947264"
        // https://www.imdb.com/title/tt11947264/?ddd=mm&dd ==> T: "tt11947264"
        // https://www.google.com/images/branding/googlelogo/2x/googlelogo_color_92x30dp.png ==> F: "googlelogo_color_92x30dp.png"
        // http://support.avantree.com/ ==> U: "support.avantree.com"
        // http://192.168.1.1:8080/ ==> U: "192.168.1.1"
        private static readonly Regex reFilename = new Regex(@"^https?:\/\/(?:(?:[^\/]+\/title\/(?<T>tt[0-9]+).*)|(?:.+\/(?<F>[a-z0-9_\.]+\.[a-z0-9]{2,4}))|(?:(?<U>[^:\/]+).*))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static Job MakeJob(string url, int jobNumber=0) // create filename from url return Job
        {
            string fn = "UNKNOWN.txt";
            var m = reFilename.Match(url);
            if (m.Success)
            {
                if (m.Groups["F"].Value.Length > 0) fn = m.Groups["F"].Value;
                else if (m.Groups["T"].Value.Length > 0) fn = m.Groups["T"].Value + ".htm";
                else if (m.Groups["U"].Value.Length > 0) fn = m.Groups["U"].Value + ".htm";
            }
            else
            {
                fn = (jobNumber > 0 ? "YY" + jobNumber.ToString("0000") : "ZZ" + Environment.TickCount.ToString("X8")) + ".txt";
            }

            var j = new Job(url, TestFolder + fn);
            j.JobNumber = jobNumber;
            return j;
        }

        public static void InitImdbUrlFileList() // Pre-extract IMDB urls to file
        {
            using (var fs = File.CreateText(ImdbUrlFile))
            {
                foreach (var url in Directory.EnumerateFiles(ImdbShortcutSourceFolder, "*.url", SearchOption.AllDirectories)
                                    .Select(m => GetUrlFromShortcut(m))
                                    .Where(m=>m.Contains("imdb.com/title/tt")))
                {
                    fs.WriteLine(url);
                }
            }
        }

        private static string GetUrlFromShortcut(string path)
        {
            using (var sr = new StreamReader(path))
            {
                string line = null;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("URL="))
                    {
                        var url = line.Substring(4).Trim();
                        //Remove urlencoded properties (e.g. ?abc=def&ccc=ddd)
                        var i = url.IndexOf('?');
                        if (i != -1) url = url.Substring(0, i);
                        return url;
                    }
                }
            }
            return "";
        }

        private static string GetProjectDir()
        {
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            var filename = Path.GetFileNameWithoutExtension(exe);
            var dir = Path.GetDirectoryName(exe);
            var contains = "\\" + filename + "\\";
            int i = dir.LastIndexOf(contains);
            if (i == -1) return dir + "\\";
            var s = dir.Substring(0, i + contains.Length);
            return s;
        }

        private static string GetTestFolder()
        {
            var tf = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), @"TestOutput\"));
            if (Directory.Exists(tf)) Directory.Delete(tf, true);
            Directory.CreateDirectory(tf);
            return tf;
        }
        #endregion
    }
}
