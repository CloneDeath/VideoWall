using System.Collections.Generic;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using VideoWall;
using VideoWall.Frames;
using VideoWall.Server;

var frameData = new[] {
	new {
		// http://www.insecam.org/en/view/454956/
		Stream = "http://166.165.35.37/mjpg/video.mjpg",
		Position = new Vector2D<float>(0, 0),
		Size = new Vector2D<float>(400, 300),
		Loading = "textures/loading1.png"
	},
	new {
		// http://www.insecam.org/en/view/989315/
		Stream = "http://104.251.136.19:8080/mjpg/video.mjpg",
		Position = new Vector2D<float>(400, 0),
		Size = new Vector2D<float>(400, 300),
		Loading = "textures/loading2.png"
	},
	new {
		// http://www.insecam.org/en/view/504145/
		Stream = "http://166.247.77.253:81/mjpg/video.mjpg",
		Position = new Vector2D<float>(0, 300),
		Size = new Vector2D<float>(400, 300),
		Loading = "textures/loading3.png"
	},
	new {
		// http://www.insecam.org/en/view/368541/
		Stream = "http://31.168.150.154:82/mjpg/video.mjpg",
		Position = new Vector2D<float>(400, 300),
		Size = new Vector2D<float>(400, 300),
		Loading = "textures/loading4.png"
	}
};

using var videoWall = new VideoWall.VideoWall(800, 600);
var cameraStreams = new List<CameraStream>();
foreach (var data in frameData) {
	var frame = new DisplayFrame(data.Position, Image.Load(data.Loading)) {
		Size = data.Size
	};
	videoWall.AddFrame(frame);

	var camera = new CameraStream(data.Stream);
	cameraStreams.Add(camera);
	camera.JpegReceived += d => frame.Image = Image.Load(d);
	camera.Start();
}
videoWall.Init();

var server = new VideoServer(videoWall);
var _ = server.Start();
var wallTask = videoWall.Run();

await wallTask;

await server.Stop();
foreach (var stream in cameraStreams) {
	stream.Stop();
}