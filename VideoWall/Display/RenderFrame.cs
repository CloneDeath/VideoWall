using SilkNetConvenience.Barriers;
using SilkNetConvenience.Buffers;
using SilkNetConvenience.CommandBuffers;
using SilkNetConvenience.Memory;

namespace VideoWall.Display; 

public class RenderFrame {
	public VulkanCommandBuffer? CommandBuffer;
	public VulkanSemaphore? ImageAvailableSemaphore;
	public VulkanSemaphore? RenderFinishedSemaphore;
	public VulkanFence? InFlightFence;

	public VulkanBuffer? UniformBuffer;
	public VulkanDeviceMemory? UniformBufferMemory;
}