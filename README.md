
# Web Data Download Testing
Three different downloaders were created to determine which is best. 

* HttpClient can be easily used in a multi-threaded environment whereas WebClient cannot. First available in .NET Framework 4.5. Major performance improvement starting in .NET Core 2.1.
* WebClient may be used in a multi-threaded environment as long as the scope of the WebClient object stays **within** a given thread. WebClient has been available since .NET Framework 1.1.
* Speed of the 2 clients appear to be very similar however HttpClient uses 78% more memory and a lot more garbage collection calls than WebClient.

Your mileage may vary.

## Developer Notes
Created with Visual Studio 2019 and .Net Framework 4.8

## Results
Download list of 40 urls alternating between IMDB Movie title and matching poster urls.
<br/><br/>

#### Asynchronous (e.g Parallelism = Infinite)
******** **Reused HttpClient** ********<br/>
Total Count:40 Duration:10.55 sec, Sum:131.37, Min:0.19, Max:9.30, Range:9.11, Ave:3.28, Median:3.16<br/>
Htm: Ave=4.00, Sum=80.44, Med=3.58<br/>
Jpg: Ave=2.64, Sum=50.16, Med=2.58<br/>
<br/>
******** **Reused WebClient** ********<br/>
Total Count:40 Duration:0.64 sec, Sum:0.90, Min:0.00, Max:0.64, Range:0.64, Ave:0.02, Median:0.00<br/>
Htm:  NotSupportedException:WebClient does not support concurrent I/O operations.<br/>
Jpg:  NotSupportedException:WebClient does not support concurrent I/O operations.<br/>
<br/>
******** **Single-use WebClient** ********<br/>
Total Count:40 Duration:7.97 sec, Sum:83.38, Min:0.05, Max:6.41, Range:6.36, Ave:2.08, Median:1.05<br/>
Htm: Ave=3.88, Sum=77.59, Med=4.23<br/>
Jpg: Ave=0.30, Sum=5.68 , Med=0.14<br/>
<br/>
******** **Single-use WebClient unmodified (mostly) from VideoLibrarian v2.4.1** ********<br/>
Total Count:40 Duration:8.81 sec, Sum:83.61, Min:0.03, Max:7.08, Range:7.05, Ave:2.09, Median:1.34<br/>
Htm: Ave=3.71, Sum=74.22, Med=3.90<br/>
Jpg: Ave=0.49, Sum=9.31, Med=0.25<br/>
<br/>
#### Synchronous (e.g Parallelism = 1)
******** **Reused HttpClient** ********<br/>
Total Count:40 Duration:19.63 sec, Sum:19.61, Min:0.02, Max:1.16, Range:1.14, Ave:0.49, Median:0.61<br/>
Htm: Ave=0.86, Sum=17.19, Med=0.90<br/>
Jpg: Ave=0.12, Sum=2.32 , Med=0.03<br/>
<br/>
******** **Reused WebClient** ********<br/>
Total Count:40 Duration:19.92 sec, Sum:19.92, Min:0.03, Max:1.16, Range:1.12, Ave:0.50, Median:0.56<br/>
Htm: Ave=0.75, Sum=15.01, Med=0.69<br/>
Jpg: (failure due to bug in code)<br/>
<br/>
******** **Single-use WebClient** ********<br/>
Total Count:40 Duration:16.56 sec, Sum:16.56, Min:0.02, Max:1.08, Range:1.06, Ave:0.41, Median:0.58<br/>
Htm: Ave=0.72, Sum=14.34, Med=0.68<br/>
Jpg: Ave=0.11, Sum=2.18, Med=0.03<br/>
<br/>
******** **Single-use WebClient unmodified (mostly) from VideoLibrarian v2.4.1** ********<br/>
Total Count:40 Duration:20.28 sec, Sum:20.28, Min:0.02, Max:2.27, Range:2.25, Ave:0.51, Median:0.63<br/>
Htm: Ave=0.91, Sum=18.13, Med=0.81<br/>
Jpg: Ave=0.11, Sum=2.08, Med=0.03<br/>
