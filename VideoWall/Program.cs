using System.Collections.Generic;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using VideoWall;
using VideoWall.Frames;
using VideoWall.Server;

var ipCams = new[] {
	// http://www.insecam.org/en/view/454956/
	"http://166.165.35.37/mjpg/video.mjpg",
	
	// http://www.insecam.org/en/view/989315/
	"http://104.251.136.19:8080/mjpg/video.mjpg",
	
	// http://www.insecam.org/en/view/504145/
	"http://166.247.77.253:81/mjpg/video.mjpg",
	
	// http://www.insecam.org/en/view/368541/
	"http://31.168.150.154:82/mjpg/video.mjpg"
};
var positions = new[] {
	new Vector3D<float>(-1, -1, 0),
	new Vector3D<float>(0, -1, 0),
	new Vector3D<float>(-1, 0, 0),
	new Vector3D<float>(0, 0, 0)
};

var frames = new List<DisplayFrame>();
var cameraStreams = new List<CameraStream>();

foreach (var ipCam in ipCams) {
	var cam = new CameraStream(ipCam);
	var img = await cam.GetSingleImage();
	var cameraFrame = new DisplayFrame(new Vector3D<float>(-1, -1, 0), Image.Load(img));
	cam.JpegReceived += d => cameraFrame.Image = Image.Load(d);
	cam.Start();
	frames.Add(cameraFrame);
	cameraStreams.Add(cam);
}

using var videoWall = new VideoWall.VideoWall();
for (var index = 0; index < frames.Count; index++) {
	var frame = frames[index];
	var position = positions[index];
	frame.Position = position;
	videoWall.AddFrame(frame);
}

//app.AddEntity(new Frame(new Vector3D<float>(0, 0, 0), Image.Load("textures/texture.jpg")));
//app.AddEntity(new Frame(new Vector3D<float>(1, 0, 0), Image.Load("textures/bird.png")));
videoWall.Init();
var wallTask = videoWall.Run();

var server = new VideoServer(videoWall);
var _ = server.Start();

await wallTask;

await server.Stop();

foreach (var stream in cameraStreams) {
	stream.Stop();
}