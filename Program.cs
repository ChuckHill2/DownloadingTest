//--------------------------------------------------------------------------
// <summary>
//   
// </summary>
// <copyright file="Program.cs" company="Chuck Hill">
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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DownloadingTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.SeverityFilter = Severity.Verbose;

            //TestDownload.TestOne("https://github.com/search?l=C%23&q=base64+stream&type=Repositories", DownloaderType.Http);
            //TestDownload.TestOne("https://www.imdb.com/title/tt11947264/", DownloaderType.Http);
            //TestDownload.TestOne("https://www.google.com/images/branding/googlelogo/2x/googlelogo_color_92x30dp.png", DownloaderType.Http);
            //TestDownload.TestOne("https://support.avantree.com/", DownloaderType.Http, useNullFilename:true);
            //TestDownload.TestOne("http://capture2text.sourceforge.net/", DownloaderType.Http);

            //TestDownload.TestOne("https://github.com/search?l=C%23&q=base64+stream&type=Repositories", DownloaderType.Web);
            //TestDownload.TestOne("https://www.imdb.com/title/tt11947264/", DownloaderType.Web);
            //TestDownload.TestOne("https://www.google.com/images/branding/googlelogo/2x/googlelogo_color_92x30dp.png", DownloaderType.Web);
            //TestDownload.TestOne("https://support.avantree.com/", DownloaderType.Web, useNullFilename: true);
            //TestDownload.TestOne("http://capture2text.sourceforge.net/", DownloaderType.Web);

            //TestDownload.TestOne("https://github.com/search?l=C%23&q=base64+stream&type=Repositories", DownloaderType.Sync);
            //TestDownload.TestOne("https://www.imdb.com/title/tt11947264/", DownloaderType.Sync);
            //TestDownload.TestOne("https://www.google.com/images/branding/googlelogo/2x/googlelogo_color_92x30dp.png", DownloaderType.Sync);
            //TestDownload.TestOne("https://support.avantree.com/", DownloaderType.Sync, useNullFilename: true);  //missing filename not supported
            //TestDownload.TestOne("http://capture2text.sourceforge.net/", DownloaderType.Sync);

            //TestDownload.TestMany(DownloadTest.ImdbUrls, DownloaderType.Http, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);
            //TestDownload.TestMany(DownloadTest.SelectUrls, DownloaderType.Http, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);

            //TestDownload.TestMany(DownloadTest.ImdbUrls, DownloaderType.Web, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);
            //TestDownload.TestMany(DownloadTest.SelectUrls, DownloaderType.Web, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);

            //TestDownload.TestMany(DownloadTest.ImdbUrls, DownloaderType.Sync, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);
            //TestDownload.TestMany(DownloadTest.SelectUrls, DownloaderType.Sync, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);

            Console.WriteLine("[Begin Test]");

            //TestDownload.TestMany(DownloadTest.ImdbAndImageUrls, DownloaderType.Http, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);
            //System.Threading.Thread.Sleep(5 * 60 * 1000);
            //TestDownload.TestMany(DownloadTest.ImdbAndImageUrls, DownloaderType.Web, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);
            //System.Threading.Thread.Sleep(5 * 60 * 1000);
            //TestDownload.TestMany(DownloadTest.ImdbAndImageUrls, DownloaderType.Sync, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);
            //System.Threading.Thread.Sleep(5 * 60 * 1000);
            //TestDownload.TestMany(DownloadTest.ImdbAndImageUrls, DownloaderType.Legacy, maxDegreeOfParallelism: int.MaxValue, urlCount: 40, doRetry: false);

            //TestDownload.TestMany(DownloadTest.ImdbAndImageUrls, DownloaderType.Http, maxDegreeOfParallelism: 1, urlCount: 40, doRetry: false);
            //System.Threading.Thread.Sleep(5 * 60 * 1000);
            //TestDownload.TestMany(DownloadTest.ImdbAndImageUrls, DownloaderType.Web, maxDegreeOfParallelism: 1, urlCount: 40, doRetry: false);
            //System.Threading.Thread.Sleep(5 * 60 * 1000);
            //TestDownload.TestMany(DownloadTest.ImdbAndImageUrls, DownloaderType.Sync, maxDegreeOfParallelism: 1, urlCount: 40, doRetry: false);
            //System.Threading.Thread.Sleep(5 * 60 * 1000);
            //TestDownload.TestMany(DownloadTest.ImdbAndImageUrls, DownloaderType.Legacy, maxDegreeOfParallelism: 1, urlCount: 40, doRetry: false);

            Console.WriteLine("[Complete]");
        }
    }
}
