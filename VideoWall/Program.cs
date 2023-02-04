using System.Collections.Generic;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using VideoWall;
using VideoWall.Display;

var ipCams = new[] {
	"http://166.165.35.37/mjpg/video.mjpg",
	"http://86.34.190.86:88/cgi-bin/faststream.jpg?stream=half&fps=15&rand=COUNTER",
	
	// http://www.insecam.org/en/view/368541/
	"http://31.168.150.154:82/mjpg/video.mjpg"
};
var positions = new[] {
	new Vector3D<float>(-1, -1, 0),
	new Vector3D<float>(0, -1, 0),
	new Vector3D<float>(-1, 0, 0)
};

var frames = new List<Frame>();
var cameraStreams = new List<CameraStream>();

foreach (var ipCam in ipCams) {
	var cam = new CameraStream(ipCam);
	var img = await cam.GetSingleImage();
	var cameraFrame = new Frame(new Vector3D<float>(-1, -1, 0), Image.Load(img));
	cam.JpegReceived += d => cameraFrame.Image = Image.Load(d);
	cam.Start();
	frames.Add(cameraFrame);
	cameraStreams.Add(cam);
}

using var app = new HelloTriangleApplication();
for (var index = 0; index < frames.Count; index++) {
	var frame = frames[index];
	var position = positions[index];
	frame.Position = position;
	app.AddEntity(frame);
}

app.AddEntity(new Frame(new Vector3D<float>(0, 0, 0), Image.Load("textures/texture.jpg")));
app.AddEntity(new Frame(new Vector3D<float>(1, 0, 0), Image.Load("textures/bird.png")));
app.Init();
app.Run();

foreach (var stream in cameraStreams) {
	stream.Stop();
}