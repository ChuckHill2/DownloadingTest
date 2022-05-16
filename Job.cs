//--------------------------------------------------------------------------
// <summary>
//   
// </summary>
// <copyright file="Job.cs" company="Chuck Hill">
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

namespace DownloadingTest
{
    /// <summary>
    /// Info to pass to WebDownloader.DownLoadAsync(), HttpDownloader.DownLoadAsync(), and Downloader.Download()
    /// </summary>
    public class Job
    {
        /// <summary>
        /// Readonly failure  counter. This counter is incremented every time the download fails.
        /// The user can re-download this job based upon the exception properties and other stateful information. The failure  counter can also tell you when to quit retrying.
        /// </summary>
        public int FailureCount { get; set; } = 0;

        /// <summary>
        /// This is just a job sequence identifier for batch/parallel debugging. Used for writing to Log.
        /// </summary>
        public int JobNumber { get; set; } = -1;  //-1 means not used

        /// <summary>
        /// Previous job url. Now the referrer to this new job.  Good for downloading a sequence of jobs.
        /// </summary>
        public string Referer { get; set; }

        /// <summary>
        /// Previously generated cookie. Now forwarded to a new job. Good for downloading a sequence of jobs.
        /// An alternate option (maybe better) option is to instaniate the downloader with the internal cookie manager option.
        /// </summary>
        public string Cookie { get; set; }

        /// <summary>
        /// Upon input, the Body of  a request for 'Post' method. This MUST be a json string (it's not validated!). 
        /// Upon input, null/empty defaults to 'Get' method.
        /// Upon successful completion, this may contain string or base64 data from the response when Job.Filename is empty.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// Absolute url path to download. Upon completion, this property is updated with the
        /// URI of the Internet resource that actually responded to the request (e.g. redirect).
        /// This readonly value must be provided in the constructor.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        ///   Full path name of file to write result to.
        ///   If file extension does not match the downloaded mimetype, the file extension is updated to match the mimetype.
        ///   Existing files are never overridden, If the file already exists, the file name is incremented (e.g 'name(nn).ext').
        ///   Upon successful completion, this property is updated with the new name and Body is ignored.
        ///   If null or invalid, results are written to Job.Body.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Readonly. Upon output, the content type of the result. 
        /// Useful when Filename is null and the Body contains base64 binary data.
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// The datetime of the resource on the server.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// The exception upon failure.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Constructor to populate this object
        /// </summary>
        /// <param name="job">Parent job info to extract the referrer and cookie from. Null if no parent.</param>
        /// <param name="url">Url to download</param>
        /// <param name="filename">
        ///   Full path name of file to write result to OR null to return result as a string in Job.Body.
        ///   If file extension does not match the downloaded mimetype, the file extension is updated to match the mimetype.
        ///   If the file already exists, the file name is incremented (e.g 'name(nn).ext')
        ///   This property is updated with the new name.
        ///   If null or invalid, results are written to this.Body.
        /// </param>
        /// <param name="body">Optional json body for the Post method and upon return. See remarks.</param>
        /// <remarks>
        /// If Job.Filename is null, the response is written to Job.Body as a string. However if the data is binary and less 
        /// than 10Mb (e.g. small image), it is Base64 encoded. If greater than 10MB, Job.Body contain an error message.
        /// </remarks>
        public Job(Job job, string url, string filename = null, string body = null)
        {
            if (job != null)
            {
                Cookie = job.Cookie;
                Referer = job.Url;
            }

            Url = url;
            Filename = filename;
            Body = body;
        }

        /// <summary>
        /// Constructor to populate this object
        /// </summary>
        /// <param name="url">Url to download</param>
        /// <param name="filename">
        ///   Full path name of file to write result to to OR null to return resulting string.
        ///   If file extension does not match the downloaded mimetype, the file extension is updated to match the mimetype.
        ///   If the file already exists, the file name is incremented (e.g 'name(nn).ext')
        ///   This property is updated with the new name.
        ///   If null or invalid, results are written to this.Body.
        /// </param>
        /// <param name="referer">Optional download referer. Simulates reference to a previous call.</param>
        /// <param name="cookie">Optional download cookie. A previously generated cookie.</param>
        /// <param name="body">Optional download body for the Post method and response body content.</param>
        public Job(string url, string filename = null, string referer = null, string cookie = null, string body = null)
        {
            Referer = referer;
            Cookie = cookie;
            Url = url;
            Filename = filename;
            Body = body;
        }

        public override string ToString() => Url??"NULL";
    }
}
