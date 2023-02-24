using System;
using System.Runtime.CompilerServices;
using Illustrate;
using Illustrate.DataObjects;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using SilkNetConvenience.Buffers;
using SilkNetConvenience.CommandBuffers;
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

	public void Initialize(GraphicsContext context, VulkanCommandPool commandPool) {
		CreateTexture(context);
		CreateUniformBuffer(context);
		CreateVertexBuffer(context, commandPool);
		CreateIndexBuffer(context, commandPool);
		Initialized = true;
	}

	private void CreateTexture(GraphicsContext context) {
		Texture = context.CreateTexture(ImageSize,
			Format.R8G8B8A8Srgb, ImageTiling.Optimal, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
			MemoryPropertyFlags.DeviceLocalBit, ImageAspectFlags.ColorBit);
	}

	private unsafe void CreateUniformBuffer(GraphicsContext context) {
		var bufferSize = (uint)sizeof(UniformBufferObject);
		UniformBuffer = context.CreateBufferMemory(bufferSize,
			BufferUsageFlags.UniformBufferBit,
			MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
	}

	private void CreateVertexBuffer(GraphicsContext context, VulkanCommandPool commandPool) {
		var bufferSize = (uint)(Unsafe.SizeOf<Vertex>() * Vertices.Length);
		
		using var stagingBuffer = context.CreateBufferMemory(bufferSize, BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
		
		var data = stagingBuffer.MapMemory<Vertex>();
		Vertices.AsSpan().CopyTo(data);
		stagingBuffer.UnmapMemory();

		VertexBuffer = context.CreateBufferMemory(bufferSize,
			BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
			MemoryPropertyFlags.DeviceLocalBit);

		CopyBuffer(context.GraphicsQueue, commandPool, stagingBuffer, VertexBuffer, bufferSize);
	}

	private void CreateIndexBuffer(GraphicsContext context, VulkanCommandPool commandPool) {
		var bufferSize =  sizeof(int) * (uint)Indices.Length;

		using var stagingBuffer = context.CreateBufferMemory(bufferSize, BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit);

		var data = stagingBuffer.MapMemory<uint>();
		Indices.AsSpan().CopyTo(data);
		stagingBuffer.UnmapMemory();

		IndexBuffer = context.CreateBufferMemory(bufferSize,
			BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
			MemoryPropertyFlags.DeviceLocalBit);

		CopyBuffer(context.GraphicsQueue, commandPool, stagingBuffer, IndexBuffer, bufferSize);
	}

	private static void CopyBuffer(VulkanQueue graphicsQueue, VulkanCommandPool commandPool, 
								   VulkanBuffer source, VulkanBuffer destination, uint size) {
		graphicsQueue.SubmitSingleUseCommandBufferAndWaitIdle(commandPool, buffer => {
			buffer.CopyBuffer(source, destination, size);
		});
	}

	public void Update(GraphicsContext context, VulkanCommandPool commandPool, Extent2D windowSize) {
		if (Texture!.Size.Width != ImageSize.Width || Texture!.Size.Height != ImageSize.Height) {
			CreateTexture(context);
		}
		Texture!.UpdateTextureImage(context.GraphicsQueue, commandPool, Image);
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