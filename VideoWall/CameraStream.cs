using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VideoWall; 

public class CameraStream {
    private readonly string _url;
    public event Action<byte[]>? JpegReceived;

    private CancellationTokenSource _tokenSource = new();

    public CameraStream(string url) {
        _url = url;
    }

    public void Start() {
        _tokenSource.Cancel();
        _tokenSource = new CancellationTokenSource();
        var token = _tokenSource.Token;
        Task.Run(() => StartGettingImages(cb => {
            JpegReceived?.Invoke(cb);
        }, token), token);
    }

    public Task<byte[]> GetSingleImage() {
        var tokenSource = new CancellationTokenSource();
        var token = tokenSource.Token;
        var source = new TaskCompletionSource<byte[]>();
        Task.Run(() => StartGettingImages(cb => {
            source.SetResult(cb);
            tokenSource.Cancel();
        }, token), token);
        return source.Task;
    }
    
	private async Task StartGettingImages(Action<byte[]> callback, CancellationToken cancellationToken) {
        const int bufSize = 1024;
        using var cli = new HttpClient();
        var streamBuffer = new byte[bufSize];

        // Give it the maximum size in bytes of your picture
        var frameBuffer = new List<byte>(1024 * 1024);

        var ff = false;
        var inPic = false;

        await using var stream = await cli.GetStreamAsync(_url, cancellationToken).ConfigureAwait(false);
        while(!cancellationToken.IsCancellationRequested) {
            var l = await stream.ReadAsync(streamBuffer.AsMemory(0, bufSize), cancellationToken).ConfigureAwait(false);
            var idx = 0;

            while(idx < l) {
                var c = streamBuffer[idx++];

                // We have found a FF
                if(c == 0xff) {
                    ff = true;
                }
                // We found a JPEG picture start
                else if(ff && c == 0xd8) {
                    frameBuffer.Clear();
                    frameBuffer.Add(0xff);
                    frameBuffer.Add(0xd8);
                    if(inPic) {
                        Console.WriteLine("Skipped frame : end expected");
                    }
                    ff = false;
                    inPic = true;
                }
                // We found a JPEG picture end
                else if(ff && c == 0xd9) {
                    frameBuffer.Add(0xff);
                    frameBuffer.Add(0xd9);

                    // Send the JPEG picture as an event
                    callback(frameBuffer.ToArray());

                    ff = false;
                    if(!inPic) {
                        Console.WriteLine("Skipped frame : start expected");
                    }
                    inPic = false;
                }
                // We are inside a JPEG picture
                else if(inPic) {
                    if(ff) {
                        frameBuffer.Add(0xff);
                        ff = false;
                    }
                    frameBuffer.Add(c);
                }
            }
        }
    }
}