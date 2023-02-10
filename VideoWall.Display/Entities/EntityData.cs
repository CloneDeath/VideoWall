using System;
using Illustrate;
using Silk.NET.Vulkan;
using Image = SixLabors.ImageSharp.Image;

namespace VideoWall.Display.Entities; 

public class EntityData {
	public Guid Id { get; } = Guid.NewGuid();
	
	private readonly IEntity _entity;
	
	public BufferMemory? VertexBuffer;
	public BufferMemory? IndexBuffer;
	
	public Texture? Texture;

	public EntityData(IEntity entity) {
		_entity = entity;
	}

	public uint[] Indices => _entity.Indices;
	public Vertex[] Vertices => _entity.Vertices;
	public Image Image => _entity.Image;
	public Extent2D ImageSize => new((uint)Image.Width, (uint)Image.Height);
}