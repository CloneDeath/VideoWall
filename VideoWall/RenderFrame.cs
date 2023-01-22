using System;
using Silk.NET.Vulkan;
using SilkNetConvenience.Wrappers;

namespace VideoWall; 

public class RenderFrame {
	public VulkanCommandBuffer? CommandBuffer;
	public Semaphore ImageAvailableSemaphore;
	public Semaphore RenderFinishedSemaphore;
	public Fence InFlightFence;

	public VulkanBuffer? UniformBuffer;
	public VulkanDeviceMemory? UniformBufferMemory;

	public VulkanDescriptorSet? DescriptorSet;
}