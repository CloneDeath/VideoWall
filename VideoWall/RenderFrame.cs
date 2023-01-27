using SilkNetConvenience.Wrappers;

namespace VideoWall; 

public class RenderFrame {
	public VulkanCommandBuffer? CommandBuffer;
	public VulkanSemaphore? ImageAvailableSemaphore;
	public VulkanSemaphore? RenderFinishedSemaphore;
	public VulkanFence? InFlightFence;

	public VulkanBuffer? UniformBuffer;
	public VulkanDeviceMemory? UniformBufferMemory;

	public VulkanDescriptorSet? DescriptorSet;
}