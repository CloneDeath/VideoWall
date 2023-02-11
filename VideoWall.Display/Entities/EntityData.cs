using System;
using System.Runtime.CompilerServices;
using Illustrate;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using SilkNetConvenience.Buffers;
using SilkNetConvenience.CommandBuffers;
using SilkNetConvenience.Devices;
using SilkNetConvenience.Queues;
using Image = SixLabors.ImageSharp.Image;

namespace VideoWall.Display.Entities; 

public class EntityData {
	public bool Initialized { get; private set; }
	
	private readonly IEntity _entity;
	
	public BufferMemory? VertexBuffer;
	public BufferMemory? IndexBuffer;
	public BufferMemory? UniformBuffer;
	public Texture? Texture;

	public EntityData(IEntity entity) {
		_entity = entity;
	}

	public uint[] Indices => _entity.Indices;
	public Vertex[] Vertices => _entity.Vertices;
	
	protected Image Image => _entity.Image;
	protected Extent2D ImageSize => new((uint)Image.Width, (uint)Image.Height);

	public void Initialize(VulkanDevice device, VulkanQueue graphicsQueue, VulkanCommandPool commandPool) {
		Texture = new Texture(device, ImageSize,
			Format.R8G8B8A8Srgb, ImageTiling.Optimal, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
			MemoryPropertyFlags.DeviceLocalBit, ImageAspectFlags.ColorBit);
		CreateUniformBuffer(device);
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
	
	private unsafe void CreateUniformBuffer(VulkanDevice device) {
		var bufferSize = (uint)sizeof(UniformBufferObject);
		UniformBuffer = new BufferMemory(device, bufferSize,
			BufferUsageFlags.UniformBufferBit,
			MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
	}

	private static void CopyBuffer(VulkanQueue graphicsQueue, VulkanCommandPool commandPool, 
								   VulkanBuffer source, VulkanBuffer destination, uint size) {
		graphicsQueue.SubmitSingleUseCommandBufferAndWaitIdle(commandPool, buffer => {
			buffer.CopyBuffer(source, destination, size);
		});
	}

	public void Update(VulkanQueue graphicsQueue, VulkanCommandPool commandPool, Extent2D windowSize) {
		Texture!.UpdateTextureImage(graphicsQueue, commandPool, Image);
		UpdateUniformBuffer(windowSize);
	}
	private void UpdateUniformBuffer(Extent2D windowSize) {
		UniformBufferObject ubo = new()
		{
			model = _entity.Model,
			view = Matrix4X4.CreateLookAt(
				new Vector3D<float>(windowSize.Width/2f, windowSize.Height/2f, -10), 
				new Vector3D<float>(windowSize.Width/2f, windowSize.Height/2f, 0), 
				new Vector3D<float>(0, -1, 0)),
			proj = Matrix4X4.CreateOrthographic(windowSize.Width, windowSize.Height, 0.1f, 100f),
		};
		ubo.proj.M22 *= -1;

		var data = UniformBuffer!.MapMemory<UniformBufferObject>();
		data[0] = ubo;
		UniformBuffer!.UnmapMemory();
	}
}