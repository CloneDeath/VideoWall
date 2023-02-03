using Silk.NET.Maths;
using VideoWall;
using VideoWall.Display;

using var app = new HelloTriangleApplication();
app.AddEntity(new Frame(new Vector3D<float>(0, 0, 0), "models/viking_room.png"));
app.AddEntity(new Frame(new Vector3D<float>(0, 0, 0.5f), "textures/texture.jpg"));
app.AddEntity(new Frame(new Vector3D<float>(0, 0, 1), "textures/bird.png"));
app.Init();
app.Run();