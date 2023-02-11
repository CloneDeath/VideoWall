using SilkNetConvenience.Barriers;
using SilkNetConvenience.CommandBuffers;

namespace VideoWall.Display; 

public class RenderFrame {
	public VulkanCommandBuffer? CommandBuffer;
	public VulkanSemaphore? ImageAvailableSemaphore;
	public VulkanSemaphore? RenderFinishedSemaphore;
	public VulkanFence? InFlightFence;
}