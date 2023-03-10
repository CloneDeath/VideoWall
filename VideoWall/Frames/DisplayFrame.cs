using System;
using Illustrate.DataObjects;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using VideoWall.Display;

namespace VideoWall.Frames; 

public class DisplayFrame : IEntity {
	public Guid Id { get; } = Guid.NewGuid();
	public Vector2D<float> Position { get; set; }
	public Vector2D<float> Size { get; set; }
	public Image Image { get; set; }

	public DisplayFrame(Vector2D<float> position, Image image) {
		Position = position;
		Size = new Vector2D<float>(image.Width, image.Height);
		Image = image;
	}

	public Matrix4X4<float> Model => Matrix4X4.CreateScale(new Vector3D<float>(Size, 1)) 
									 * Matrix4X4.CreateTranslation(new Vector3D<float>(Position, 0));
	public Vertex[] Vertices => new Vertex[] {
		new() {
			Position = new Vector3D<float>(0, 0, 0),
			TexCoord = new Vector2D<float>(0, 0),
			Color = new Vector3D<float>(1)
		},
		new() {
			Position = new Vector3D<float>(1, 0, 0),
			TexCoord = new Vector2D<float>(1, 0),
			Color = new Vector3D<float>(1)
		},
		new() {
			Position = new Vector3D<float>(1, 1, 0),
			TexCoord = new Vector2D<float>(1, 1),
			Color = new Vector3D<float>(1)
		},
		new() {
			Position = new Vector3D<float>(0, 1, 0),
			TexCoord = new Vector2D<float>(0, 1),
			Color = new Vector3D<float>(1)
		},
	};
	public uint[] Indices { get; } = {0, 1, 2, 2, 3, 0};
}