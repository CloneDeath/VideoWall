using Silk.NET.Maths;
using SixLabors.ImageSharp;
using VideoWall.Display;

namespace VideoWall; 

public class Frame : IEntity {
	private readonly Vector3D<float> _offset;

	public Image Image { get; }

	public Frame(Vector3D<float> offset, Image image) {
		_offset = offset;
		Image = image;
	}

	public Vertex[] Vertices => new Vertex[] {
		new() {
			Position = new Vector3D<float>(0, 0, 0) + _offset,
			TexCoord = new Vector2D<float>(0, 0),
			Color = new Vector3D<float>(1)
		},
		new() {
			Position = new Vector3D<float>(1, 0, 0) + _offset,
			TexCoord = new Vector2D<float>(1, 0),
			Color = new Vector3D<float>(1)
		},
		new() {
			Position = new Vector3D<float>(1, 1, 0) + _offset,
			TexCoord = new Vector2D<float>(1, 1),
			Color = new Vector3D<float>(1)
		},
		new() {
			Position = new Vector3D<float>(0, 1, 0) + _offset,
			TexCoord = new Vector2D<float>(0, 1),
			Color = new Vector3D<float>(1)
		},
	};
	public uint[] Indices { get; } = {0, 1, 2, 2, 3, 0};
}