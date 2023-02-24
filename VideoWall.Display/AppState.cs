using System;
using Illustrate;
using SilkNetConvenience.CommandBuffers;
using SilkNetConvenience.Images;
using SilkNetConvenience.Queues;

namespace VideoWall.Display; 

public class AppState : IDisposable {
	private readonly GraphicsContext _context;
	public VulkanQueue GraphicsQueue => _context.GraphicsQueue;
	public VulkanQueue PresentQueue => _context.PresentQueue;
	public VulkanCommandPool CommandPool { get; }
	public GraphicsPipelineContext GraphicsPipeline { get; }
	public VulkanSampler Sampler { get; }

	public AppState(GraphicsContext context, VulkanCommandPool commandPool,
					GraphicsPipelineContext graphicsPipeline, VulkanSampler sampler) {
		_context = context;
		CommandPool = commandPool;
		GraphicsPipeline = graphicsPipeline;
		Sampler = sampler;
	}

	public void Dispose() {
		_context.Dispose();
		GC.SuppressFinalize(this);
	}

	~AppState() => Dispose();
}