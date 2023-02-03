using System;
using SilkNetConvenience.CommandBuffers;
using SilkNetConvenience.Devices;
using SilkNetConvenience.Instances;
using SilkNetConvenience.KHR;
using SilkNetConvenience.Pipelines;
using SilkNetConvenience.Queues;
using SilkNetConvenience.RenderPasses;

namespace VideoWall; 

public class AppState : IDisposable {
	public VulkanInstance Instance { get; }
	public VulkanPhysicalDevice PhysicalDevice { get; }
	public VulkanDevice Device { get; }
	public VulkanQueue GraphicsQueue { get; }
	public VulkanQueue PresentQueue { get; }
	public VulkanSurface Surface { get; }
	public VulkanCommandPool CommandPool { get; }
	public VulkanRenderPass RenderPass { get; }
	public VulkanPipelineLayout PipelineLayout { get; }
	public VulkanPipeline GraphicsPipeline { get; }

	public AppState(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanDevice device, 
					VulkanQueue graphicsQueue, VulkanQueue presentQueue, VulkanSurface surface, 
					VulkanCommandPool commandPool, VulkanRenderPass renderPass, VulkanPipelineLayout pipelineLayout, 
					VulkanPipeline graphicsPipeline) {
		Instance = instance;
		PhysicalDevice = physicalDevice;
		Device = device;
		GraphicsQueue = graphicsQueue;
		PresentQueue = presentQueue;
		Surface = surface;
		CommandPool = commandPool;
		RenderPass = renderPass;
		PipelineLayout = pipelineLayout;
		GraphicsPipeline = graphicsPipeline;
	}

	public void Dispose() {
		Instance.Dispose();
		GC.SuppressFinalize(this);
	}

	~AppState() => Dispose();
}