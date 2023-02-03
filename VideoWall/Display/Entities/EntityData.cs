using SilkNetConvenience.Buffers;
using SilkNetConvenience.Images;
using SilkNetConvenience.Memory;
using SixLabors.ImageSharp;

namespace VideoWall.Display.Entities; 

public class EntityData {
	private readonly IEntity _entity;
	
	public VulkanBuffer? VertexBuffer;
	public VulkanDeviceMemory? VertexBufferMemory;
	
	public VulkanBuffer? IndexBuffer;
	public VulkanDeviceMemory? IndexBufferMemory;

	public VulkanImage? Texture;
	public VulkanDeviceMemory? TextureMemory;
	public VulkanImageView? TextureImageView;

	public EntityData(IEntity entity) {
		_entity = entity;
	}

	public uint[] Indices => _entity.Indices;
	public Vertex[] Vertices => _entity.Vertices;
	public Image Image => _entity.Image;
}