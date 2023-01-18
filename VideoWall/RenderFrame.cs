using Silk.NET.Vulkan;
using SilkNetConvenience.Wrappers;

namespace VideoWall; 

public class RenderFrame {
	public CommandBuffer CommandBuffer;
	public Semaphore ImageAvailableSemaphore;
	public Semaphore RenderFinishedSemaphore;
	public Fence InFlightFence;

	public VulkanBuffer UniformBuffer;
	public VulkanDeviceMemory UniformBufferMemory;
	public object UniformBufferMapped;
}