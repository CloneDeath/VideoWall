using Silk.NET.Vulkan;

namespace VideoWall; 

public class RenderFrame {
	public CommandBuffer CommandBuffer;
	public Semaphore ImageAvailableSemaphore;
	public Semaphore RenderFinishedSemaphore;
	public Fence InFlightFence;
}