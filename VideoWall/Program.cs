using Silk.NET.Maths;
using SixLabors.ImageSharp;
using VideoWall;
using VideoWall.Display;

var cam = new CameraStream("http://166.165.35.37/mjpg/video.mjpg");
var img = await cam.GetSingleImage();

using var app = new HelloTriangleApplication();
app.AddEntity(new Frame(new Vector3D<float>(0, 0, 0), Image.Load(img)));
app.AddEntity(new Frame(new Vector3D<float>(0, 0, 0.5f), Image.Load("textures/texture.jpg")));
app.AddEntity(new Frame(new Vector3D<float>(0, 0, 1), Image.Load("textures/bird.png")));
app.Init();
app.Run();