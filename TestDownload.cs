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

    public static class TestDownload
    {
        /// <summary>
        /// One shot test for a specified URL
        /// </summary>
        /// <param name="url">url to download</param>
        /// <param name="downloaderType">Which downloader to use.</param>
        /// <param name="useNullFilename">True to just retrieve data into Job.Body</param>
        public static void TestOne(string url, DownloaderType downloaderType, bool useNullFilename = false)
        {
            var job = useNullFilename ? new Job(url) : TestCommon.MakeJob(url);

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
                else if (downloaderType == DownloaderType.Legacy)
                {
                    download = j => LegacyDownloader.Download(j);
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
            IEnumerable<string> urls = TestCommon.GetUrls(test, urlCount);

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
            downloadJobs = urls.Select(url => TestCommon.MakeJob(url, jobNumber++)).ToList();
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
                else if (downloaderType == DownloaderType.Legacy)
                {
                    download = j => LegacyDownloader.Download(j);
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
    }
}
