using SilkNetConvenience.Buffers;
using SilkNetConvenience.Memory;

namespace VideoWall.Display.Entities; 

public class EntityData {
	private readonly IEntity _entity;
	
	public VulkanBuffer? VertexBuffer;
	public VulkanDeviceMemory? VertexBufferMemory;
	
	public VulkanBuffer? IndexBuffer;
	public VulkanDeviceMemory? IndexBufferMemory;

	public EntityData(IEntity entity) {
		_entity = entity;
	}

	public uint[] Indices => _entity.Indices;
	public Vertex[] Vertices => _entity.Vertices;
}