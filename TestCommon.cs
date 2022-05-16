using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DownloadingTest
{
    public enum DownloaderType 
    { 
        /// <summary>
        /// Reused WebClient
        /// </summary>
        Web, 
        /// <summary>
        /// Reused HttpClient
        /// </summary>
        Http, 
        /// <summary>
        /// Single-use WebClient
        /// </summary>
        Sync, 
        /// <summary>
        /// Single-use WebClient unmodified (mostly) from VideoLibrarian v2.4.1
        /// </summary>
        Legacy
    }

    public enum DownloadTest 
    {
        /// <summary>
        /// List of miscellaneous urls retrieved online.
        /// </summary>
        MiscUrls, 
        /// <summary>
        /// List of only IMDB Movie title urls
        /// </summary>
        ImdbUrls,
        /// <summary>
        /// Small list of a subset of miscellaneous urls that exercise most of the code (hopefully).
        /// </summary>
        SelectUrls,
        /// <summary>
        /// Alternating list of IMDB Movie title and matching poster urls. Mimics VideoLibrarian downloads.
        /// </summary>
        ImdbAndImageUrls
    }

    public class TestCommon
    {
        public static readonly string TestFolder = GetTestFolder();

        private static readonly string ProjectDir = GetProjectDir();
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

        // Regex pattern parse results:
        // https://github.com/search?l=C%23&q=base64+stream&type=Repositories ==> U: "github.com"
        // https://www.imdb.com/title/tt11947264 ==> T: "tt11947264"
        // https://www.imdb.com/title/tt11947264/ ==> T: "tt11947264"
        // https://www.imdb.com/title/tt11947264/?ddd=mm&dd ==> T: "tt11947264"
        // https://www.google.com/images/branding/googlelogo/2x/googlelogo_color_92x30dp.png ==> F: "googlelogo_color_92x30dp.png"
        // http://support.avantree.com/ ==> U: "support.avantree.com"
        // http://192.168.1.1:8080/ ==> U: "192.168.1.1"
        private static readonly Regex reFilename = new Regex(@"^https?:\/\/(?:(?:[^\/]+\/title\/(?<T>tt[0-9]+).*)|(?:.+\/(?<F>[a-z0-9_\.]+\.[a-z0-9]{2,4}))|(?:(?<U>[^:\/]+).*))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Job MakeJob(string url, int jobNumber = 0) // create filename from url return Job
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

        public static IEnumerable<string> GetUrls(DownloadTest test, int count)
        {
            if (test == DownloadTest.SelectUrls)
                return CustomTestUrls.Take(count);

            var urlFile = $"{ProjectDir}Resources\\{test}.txt";
            return File.ReadAllLines(urlFile).Where(m => m.StartsWith("http")).Take(count);
        }

        public static void InitImdbUrlFileList() // Pre-extract IMDB urls to file
        {
            const string ImdbShortcutSourceFolder = @"C:\Users\User\source\repos\VideoLibrarian\TestVideos.Master.X";
            string ImdbUrlFile = ProjectDir + @"Resources\ImdbUrls.txt";

            using (var fs = File.CreateText(ImdbUrlFile))
            {
                foreach (var url in Directory.EnumerateFiles(ImdbShortcutSourceFolder, "*.url", SearchOption.AllDirectories)
                                    .Select(m => GetUrlFromShortcut(m))
                                    .Where(m => m.Contains("imdb.com/title/tt")))
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
    }
}
