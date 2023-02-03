using Silk.NET.Maths;
using VideoWall;
using VideoWall.Display;

using var app = new HelloTriangleApplication();
app.AddEntity(new Frame(new Vector3D<float>(0)));
app.AddEntity(new Frame(new Vector3D<float>(0,0, 1)));
app.Init();
app.Run();