using Silk.NET.Maths;

namespace VideoWall.Server.Controllers; 

public interface IFrame {
	public Guid Id { get; }
	public Vector3D<float> Position { get; }
}