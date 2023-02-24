using Illustrate.DataObjects;
using Silk.NET.Maths;
using SixLabors.ImageSharp;

namespace VideoWall.Display; 

public interface IEntity {
	public Vertex[] Vertices { get; }
	public uint[] Indices { get; }
	public Image Image { get; }
	public Matrix4X4<float> Model { get; }
}