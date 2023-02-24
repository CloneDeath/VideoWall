using System;
using System.Collections.Generic;
using System.Linq;
using Illustrate;
using Illustrate.Factories;
using Silk.NET.Vulkan;
using SilkNetConvenience.Buffers;
using SilkNetConvenience.CommandBuffers;
using SilkNetConvenience.Exceptions.ResultExceptions;
using SilkNetConvenience.Images;
using SilkNetConvenience.Queues;
using VideoWall.Display.Entities;
using File = System.IO.File;

namespace VideoWall.Display; 

public class VideoWallApplication : IDisposable
{
	private const int MaxFramesInFlight = 2;
	
	public bool EnableValidationLayers = true;

	private readonly Window window;
	private readonly GraphicsContext context;
	private AppState? _appState;

	private SwapchainContext? _swapchainContext; 

	private VulkanFramebuffer[] swapchainFramebuffers = Array.Empty<VulkanFramebuffer>();

	private readonly RenderFrame[] renderFrames = new RenderFrame[MaxFramesInFlight];
	private int currentFrame;

	private bool framebufferResized;

	private Texture? depth;

	public VideoWallApplication(int width, int height) {
		window = new Window("VideoWall", width, height);
		if (window.VkSurface is null)
		{
			throw new Exception("Windowing platform doesn't support Vulkan.");
		}

		context = GraphicsContextFactory.Create(new GraphicsContextCreateInfo {
			ApplicationName = "VideoWall",
			EnableValidation = true,
			Window = window
		});
	}

	public void Init() {
		_appState = InitVulkan();
	}

	public void Run() {
		if (_appState == null) throw new Exception("Need to run Init first");
		
		MainLoop(_appState);
	}

	private AppState InitVulkan() {
		var commandPool = context.CreateGraphicsCommandPool();
		for (var i = 0; i < MaxFramesInFlight; i++) {
			renderFrames[i] = new RenderFrame(context, commandPool);
		}

		CreateSwapchain();
		
		var vertShaderCode = File.ReadAllBytes("shaders/vert.spv");
		var fragShaderCode = File.ReadAllBytes("shaders/frag.spv");
		var graphicsPipeline = context.CreateGraphicsPipeline(vertShaderCode, fragShaderCode, 
			_swapchainContext!.ColorFormat, _swapchainContext.OutputSize);
		
		CreateDepthResources(commandPool);
		CreateFramebuffers(graphicsPipeline);

		var sampler = context.CreateTextureSampler();

		return new AppState(context, commandPool, graphicsPipeline, sampler);
	}
	

	private void CreateDepthResources(VulkanCommandPool commandPool) {
		depth = context.CreateDepthTexture(commandPool, _swapchainContext!.OutputSize);
	}

	private void RecreateSwapchain(GraphicsPipelineContext graphicsPipeline, VulkanCommandPool commandPool) {
		Extent2D framebufferSize;
		do {
			framebufferSize = window.FramebufferSize;
			window.DoEvents();
		} while (framebufferSize.Width == 0 || framebufferSize.Height == 0);

		context.WaitIdle();
		
		CleanupSwapchain();
		
		CreateSwapchain();
		CreateDepthResources(commandPool);
		CreateFramebuffers(graphicsPipeline);
	}

	private void CleanupSwapchain() {
		depth!.Dispose();
		
		foreach (var swapchainFramebuffer in swapchainFramebuffers) {
			swapchainFramebuffer.Dispose();
		}
		_swapchainContext!.Dispose();
	}

	private void CreateSwapchain() {
		_swapchainContext = context.CreateSwapchain(window.FramebufferSize);
	}

	private void CreateFramebuffers(GraphicsPipelineContext pipelineContext) {
		swapchainFramebuffers = _swapchainContext!.ImageViews.Select(image =>
													 pipelineContext.CreateFramebuffer(image, depth!.ImageView,
														 _swapchainContext.OutputSize))
												 .ToArray();
	}

	private void RecordCommandBuffer(VulkanCommandBuffer cmd, VulkanFramebuffer framebuffer, 
									 GraphicsPipelineContext graphicsPipeline, VulkanSampler sampler) {
		cmd.Begin();
		
		graphicsPipeline.BeginRenderPass(cmd, framebuffer, sampler, _swapchainContext!.OutputSize);

		foreach (var entity in _entities) {
			var set = context.UpdateDescriptorSet((uint)currentFrame, entity.UniformBuffer!, entity.Texture!.ImageView, sampler);

			cmd.BindVertexBuffer(0, entity.VertexBuffer!);
			cmd.BindIndexBuffer(entity.IndexBuffer!, 0, IndexType.Uint32);
			graphicsPipeline.BindDescriptorSet(cmd, set);
			cmd.DrawIndexed((uint)entity.Indices.Length);
		}
		
		cmd.EndRenderPass();
		
		cmd.End();
	}

	private void MainLoop(AppState appState) {
		window.Render += _ => DrawFrame(appState);
		window.Resize += _ => framebufferResized = true;
		window.Run();
		context.WaitIdle();
	}

	private void DrawFrame(AppState appState) {
		var frame = renderFrames[currentFrame];
		frame.InFlightFence!.Wait();

		uint imageIndex;
		try {
			imageIndex = _swapchainContext!.AcquireNextImage(frame.ImageAvailableSemaphore!);
		}
		catch (ErrorOutOfDateKhrException)
		{
			RecreateSwapchain(appState.GraphicsPipeline, appState.CommandPool);
			return;
		}
		catch (SuboptimalKhrException)
		{
			RecreateSwapchain(appState.GraphicsPipeline, appState.CommandPool);
			return;
		}

		frame.InFlightFence.Reset();
		frame.CommandBuffer!.Reset();
		foreach (var entity in _entities) {
			if (!entity.Initialized) {
				entity.Initialize(context, appState.CommandPool);
			}

			entity.Update(context, appState.CommandPool, _swapchainContext.OutputSize);
		}

		var framebuffer = swapchainFramebuffers[imageIndex];
		RecordCommandBuffer(frame.CommandBuffer, framebuffer, appState.GraphicsPipeline, appState.Sampler);

		var buffer = frame.CommandBuffer;
		var signalSemaphores = new[] { frame.RenderFinishedSemaphore!.Semaphore };

		appState.GraphicsQueue.Submit(new SubmitInformation {
			WaitSemaphores = new[]{ frame.ImageAvailableSemaphore!.Semaphore },
			SignalSemaphores = signalSemaphores,
			CommandBuffers = new[]{buffer.CommandBuffer},
			WaitDstStageMask = new[] { PipelineStageFlags.ColorAttachmentOutputBit }
		}, frame.InFlightFence);

		try {
			_swapchainContext.QueuePresent(signalSemaphores, imageIndex);
		}
		catch (ErrorOutOfDateKhrException) {
			framebufferResized = false;
			RecreateSwapchain(appState.GraphicsPipeline, appState.CommandPool);
			return;
		}
		catch (SuboptimalKhrException) {
			framebufferResized = false;
			RecreateSwapchain(appState.GraphicsPipeline, appState.CommandPool);
			return;
		}
		if (framebufferResized) {
			framebufferResized = false;
			RecreateSwapchain(appState.GraphicsPipeline, appState.CommandPool);
			return;
		}
		currentFrame = (currentFrame + 1) % MaxFramesInFlight;
	}

	public void Dispose() {
		foreach (var frame in renderFrames) {
			frame.ImageAvailableSemaphore!.Dispose();
			frame.RenderFinishedSemaphore!.Dispose();
			frame.InFlightFence!.Dispose();
		}

		CleanupSwapchain();

		_appState!.Dispose();
		context.Dispose();
		window.Dispose();
		GC.SuppressFinalize(this);
	}

	~VideoWallApplication() {
		Dispose();
	}

	private readonly List<EntityData> _entities = new();
	public void AddEntity(IEntity entity) {
		_entities.Add(new EntityData(entity));
	}
}