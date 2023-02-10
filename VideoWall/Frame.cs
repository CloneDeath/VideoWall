using System;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using VideoWall.Display;
using VideoWall.Server.Controllers;

namespace VideoWall; 

public class Frame : IEntity, IFrame {
	public Guid Id { get; } = Guid.NewGuid();
	public Vector3D<float> Position { get; set; }

	public Image Image { get; set; }

	public Frame(Vector3D<float> position, Image image) {
		Position = position;
		Image = image;
	}

	public Vertex[] Vertices => new Vertex[] {
		new() {
			Position = new Vector3D<float>(0, 0, 0) + Position,
			TexCoord = new Vector2D<float>(0, 0),
			Color = new Vector3D<float>(1)
		},
		new() {
			Position = new Vector3D<float>(1, 0, 0) + Position,
			TexCoord = new Vector2D<float>(1, 0),
			Color = new Vector3D<float>(1)
		},
		new() {
			Position = new Vector3D<float>(1, 1, 0) + Position,
			TexCoord = new Vector2D<float>(1, 1),
			Color = new Vector3D<float>(1)
		},
		new() {
			Position = new Vector3D<float>(0, 1, 0) + Position,
			TexCoord = new Vector2D<float>(0, 1),
			Color = new Vector3D<float>(1)
		},
	};
	public uint[] Indices { get; } = {0, 1, 2, 2, 3, 0};
}