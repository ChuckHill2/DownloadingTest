
# Web Data Download Testing
Three different downloaders were created to determine which is best. 

* HttpClient can be easily used in a multi-threaded environment whereas WebClient cannot.
* WebClient may be used in a multi-threaded environment as long as the scope of the WebClient object stays within a given thread.
* WebClient async API appear slower than Http async api.
* WebClient synchronous API appear as fast as Http async api when used against a non-blocking server.
* Async HttpClient uses 78% more memory and a lot of garbage collection calls than synchronous WebClient.

Results are logged to a log file.

Your mileage may vary. 

## Developer Notes
Created with Visual Studio 2019 and .Net Framework 4.8
