using System;
using System.Runtime.CompilerServices;
using Illustrate;
using Silk.NET.Vulkan;
using SilkNetConvenience.Buffers;
using SilkNetConvenience.CommandBuffers;
using SilkNetConvenience.Devices;
using SilkNetConvenience.Queues;
using Image = SixLabors.ImageSharp.Image;

namespace VideoWall.Display.Entities; 

public class EntityData {
	public Guid Id { get; } = Guid.NewGuid();
	public bool Initialized { get; private set; }
	
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

	public void Initialize(VulkanDevice device, VulkanQueue graphicsQueue, VulkanCommandPool commandPool) {
		Texture = new Texture(device, ImageSize,
			Format.R8G8B8A8Srgb, ImageTiling.Optimal, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
			MemoryPropertyFlags.DeviceLocalBit, ImageAspectFlags.ColorBit);

		CreateVertexBuffer(device, graphicsQueue, commandPool);
		CreateIndexBuffer(device, graphicsQueue, commandPool);
		Initialized = true;
	}
	
	private void CreateVertexBuffer(VulkanDevice device, VulkanQueue graphicsQueue, VulkanCommandPool commandPool) {
		var bufferSize = (uint)(Unsafe.SizeOf<Vertex>() * Vertices.Length);
		
		using var stagingBuffer = new BufferMemory(device, bufferSize, BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
		
		var data = stagingBuffer.MapMemory<Vertex>();
		Vertices.AsSpan().CopyTo(data);
		stagingBuffer.UnmapMemory();

		VertexBuffer = new BufferMemory(device, bufferSize,
			BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
			MemoryPropertyFlags.DeviceLocalBit);

		CopyBuffer(graphicsQueue, commandPool, stagingBuffer, VertexBuffer, bufferSize);
	}

	private void CreateIndexBuffer(VulkanDevice device, VulkanQueue graphicsQueue, VulkanCommandPool commandPool) {
		var bufferSize =  sizeof(int) * (uint)Indices.Length;

		using var stagingBuffer = new BufferMemory(device, bufferSize, BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit);

		var data = stagingBuffer.MapMemory<uint>();
		Indices.AsSpan().CopyTo(data);
		stagingBuffer.UnmapMemory();

		IndexBuffer = new BufferMemory(device, bufferSize,
			BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
			MemoryPropertyFlags.DeviceLocalBit);

		CopyBuffer(graphicsQueue, commandPool, stagingBuffer, IndexBuffer, bufferSize);
	}

	private static void CopyBuffer(VulkanQueue graphicsQueue, VulkanCommandPool commandPool, 
								   VulkanBuffer source, VulkanBuffer destination, uint size) {
		graphicsQueue.SubmitSingleUseCommandBufferAndWaitIdle(commandPool, buffer => {
			buffer.CopyBuffer(source, destination, size);
		});
	}
}