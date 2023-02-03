using SixLabors.ImageSharp;
using VideoWall.Display;

namespace VideoWall; 

public interface IEntity {
	public Vertex[] Vertices { get; }
	public uint[] Indices { get; }
	public Image Image { get; }
}