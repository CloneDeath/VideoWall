using Illustrate;
using Silk.NET.Vulkan;
using SilkNetConvenience.Barriers;
using SilkNetConvenience.CommandBuffers;

namespace VideoWall.Display; 

public class RenderFrame {
	public VulkanCommandBuffer? CommandBuffer;
	public VulkanSemaphore? ImageAvailableSemaphore;
	public VulkanSemaphore? RenderFinishedSemaphore;
	public VulkanFence? InFlightFence;

	public RenderFrame(GraphicsContext context, VulkanCommandPool commandPool) {
		ImageAvailableSemaphore = context.CreateSemaphore();
		RenderFinishedSemaphore = context.CreateSemaphore();
		InFlightFence = context.CreateFence(FenceCreateFlags.SignaledBit);
		CommandBuffer = commandPool.AllocateCommandBuffer();
	}
}