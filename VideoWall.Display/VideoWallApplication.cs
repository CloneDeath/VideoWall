using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Illustrate;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using SilkNetConvenience;
using SilkNetConvenience.Buffers;
using SilkNetConvenience.CommandBuffers;
using SilkNetConvenience.Devices;
using SilkNetConvenience.Exceptions.ResultExceptions;
using SilkNetConvenience.EXT;
using SilkNetConvenience.Images;
using SilkNetConvenience.Instances;
using SilkNetConvenience.KHR;
using SilkNetConvenience.Pipelines;
using SilkNetConvenience.Queues;
using SilkNetConvenience.RenderPasses;
using VideoWall.Display.Descriptors;
using VideoWall.Display.Entities;
using VideoWall.Display.Exceptions;
using File = System.IO.File;

namespace VideoWall.Display; 

public class VideoWallApplication : IDisposable
{
	private const int MaxFramesInFlight = 2;

	private readonly string[] ValidationLayers = {
		"VK_LAYER_KHRONOS_validation"
	};
	public bool EnableValidationLayers = true;

	private readonly string[] DeviceExtensions = {
		KhrSwapchain.ExtensionName
	};

	private readonly Window window;
	private readonly VulkanContext vk;
	private AppState? _appState;
	
	private VulkanSwapchain? swapchain;
	private VulkanSwapchainImage[] swapchainImages = Array.Empty<VulkanSwapchainImage>();
	private Format swapchainFormat;
	private Extent2D swapchainExtent;

	private VulkanImageView[] swapchainImageViews = Array.Empty<VulkanImageView>();
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
		
		vk = new VulkanContext();
	}

	public void Init() {
		_appState = InitVulkan();
	}

	public void Run() {
		if (_appState == null) throw new Exception("Need to run Init first");
		
		MainLoop(_appState);
	}

	private AppState InitVulkan() {
		for (var i = 0; i < MaxFramesInFlight; i++) {
			renderFrames[i] = new RenderFrame();
		}
		
		var instance = CreateInstance();
		SetupDebugMessenger(instance);
		var surface = CreateSurface(instance);
		var physicalDevice = PickPhysicalDevice(instance, surface);
		var device = CreateLogicalDevice(instance, physicalDevice, surface);
		var graphicsQueue = GetGraphicsQueue(instance, physicalDevice, device, surface);
		var presentQueue = GetPresentQueue(instance, physicalDevice, device, surface);
		CreateSwapchain(instance, physicalDevice, device, surface);
		CreateImageViews(device);
		var renderPass = CreateRenderPass(physicalDevice, device);
		var descriptorManager = new DescriptorManager(device);
		var pipelineLayout = device.CreatePipelineLayout(descriptorManager.DescriptorSetLayout);
		var graphicsPipeline = CreateGraphicsPipeline(device, renderPass, pipelineLayout);
		var commandPool = CreateCommandPool(instance, physicalDevice, device, surface);
		CreateDepthResources(physicalDevice, device, graphicsQueue, commandPool);
		CreateFramebuffers(device, renderPass);
		
		var sampler = CreateTextureSampler(physicalDevice, device);

		CreateCommandBuffers(commandPool);
		CreateSyncObjects(device);

		return new AppState(instance, physicalDevice, device, graphicsQueue, presentQueue, surface, commandPool, renderPass, 
				   pipelineLayout, graphicsPipeline, sampler, descriptorManager);
	}
	

	private void CreateDepthResources(VulkanPhysicalDevice physicalDevice, VulkanDevice device, 
									  VulkanQueue graphicsQueue, VulkanCommandPool commandPool) {
		var depthFormat = FindDepthFormat(physicalDevice);
		depth = new Texture(device, swapchainExtent, depthFormat, ImageTiling.Optimal,
			ImageUsageFlags.DepthStencilAttachmentBit, MemoryPropertyFlags.DeviceLocalBit, ImageAspectFlags.DepthBit);

		depth.TransitionImageLayout(graphicsQueue, depthFormat, 
			ImageLayout.Undefined, ImageLayout.DepthStencilAttachmentOptimal, commandPool);
	}

	private Format FindDepthFormat(VulkanPhysicalDevice physicalDevice) {
		return FindSupportedFormat(physicalDevice, new[] {
			Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint
		}, ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachmentBit);
	}

	private Format FindSupportedFormat(VulkanPhysicalDevice physicalDevice, 
									   IEnumerable<Format> candidates, ImageTiling tiling, FormatFeatureFlags features) {
		foreach (var format in candidates) {
			var properties = physicalDevice.GetFormatProperties(format);
			if (tiling == ImageTiling.Linear && (properties.OptimalTilingFeatures & features) == features) {
				return format;
			}
			if (tiling == ImageTiling.Optimal && (properties.OptimalTilingFeatures & features) == features) {
				return format;
			}
		}

		throw new Exception("Could not find a suitable format");
	}

	private VulkanSampler CreateTextureSampler(VulkanPhysicalDevice physicalDevice, VulkanDevice device) {
		var properties = physicalDevice.GetProperties();
		return device.CreateSampler(new SamplerCreateInformation {
			MinFilter = Filter.Linear,
			MagFilter = Filter.Linear,
			AddressModeU = SamplerAddressMode.Repeat,
			AddressModeV = SamplerAddressMode.Repeat,
			AddressModeW = SamplerAddressMode.Repeat,
			AnisotropyEnable = true,
			MaxAnisotropy = properties.Limits.MaxSamplerAnisotropy,
			BorderColor = BorderColor.IntOpaqueBlack,
			UnnormalizedCoordinates = false,
			CompareEnable = false,
			CompareOp = CompareOp.Always,
			MipmapMode = SamplerMipmapMode.Linear,
			MipLodBias = 0,
			MinLod = 0,
			MaxLod = 0
		});
	}

	private VulkanInstance CreateInstance() {
		if (EnableValidationLayers && !CheckValidationLayerSupport()) {
			throw new Exception("Validation layers not found");
		}
		var appInfo = new ApplicationInformation {
			ApplicationName = "Hello Triangle",
			ApplicationVersion = Vk.MakeVersion(1, 0),
			EngineName = "No Engine",
			EngineVersion = Vk.MakeVersion(1, 0),
			ApiVersion = Vk.MakeVersion(1, 0)
		};

		var extensions = GetRequiredExtensions();
		var createInfo = new InstanceCreateInformation {
			ApplicationInfo = appInfo,
			EnabledExtensions = extensions
		};
		
		DebugUtilsMessengerCreateInformation debugCreateInfo = new ();
		if (EnableValidationLayers) {
			createInfo.EnabledLayers = ValidationLayers;

			PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
			createInfo.DebugUtilsMessengerCreateInfo = debugCreateInfo;
		}

		return vk.CreateInstance(createInfo);
	}

	private unsafe string[] GetRequiredExtensions() {
		var glfwExtensions = window.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
		var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);

		return EnableValidationLayers 
			? extensions.Append(ExtDebugUtils.ExtensionName).ToArray() 
			: extensions;
	}

	private bool CheckValidationLayerSupport() {
		var availableLayers = vk.EnumerateInstanceLayerProperties();
		var availableLayerNames = availableLayers.Select(layer => layer.GetLayerName()).ToHashSet();
		return ValidationLayers.All(availableLayerNames.Contains);
	}

	private void SetupDebugMessenger(VulkanInstance instance) {
		if (!EnableValidationLayers) return;

		var createInfo = new DebugUtilsMessengerCreateInformation();
		PopulateDebugMessengerCreateInfo(ref createInfo);

		instance.DebugUtils.CreateDebugUtilsMessenger(createInfo);
	}

	private static unsafe void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInformation createInfo) {
		createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt
		                             | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt;
		createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
		                         | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
		                         | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
		createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
	}

	private VulkanSurface CreateSurface(VulkanInstance instance) {
		return instance.KhrSurface.CreateSurface(window.VkSurface!);
	}

	private VulkanPhysicalDevice PickPhysicalDevice(VulkanInstance instance, VulkanSurface surface) {
		return instance.PhysicalDevices.First(d => IsDeviceSuitable(instance, d, surface));
	}

	private bool IsDeviceSuitable(VulkanInstance instance, VulkanPhysicalDevice device, VulkanSurface surface) {
		var queueFamilyIndices = FindQueueFamilies(instance, device, surface);
		var extensionsSupported = CheckDeviceExtensionSupport(device);

		var swapchainAdequate = false;
		if (extensionsSupported) {
			var swapchainSupport = QuerySwapchainSupport(instance, device, surface);
			swapchainAdequate = swapchainSupport.Formats.Any() && swapchainSupport.PresentModes.Any();
		}

		var supportedFeatures = device.GetFeatures();
		
		return queueFamilyIndices.IsComplete() && extensionsSupported && swapchainAdequate && supportedFeatures.SamplerAnisotropy;
	}

	private bool CheckDeviceExtensionSupport(VulkanPhysicalDevice physicalDevice) {
		var properties = physicalDevice.EnumerateExtensionProperties();
		var propertyNames = properties.Select(p => p.GetExtensionName()).ToList();
		return DeviceExtensions.All(propertyNames.Contains);
	}

	private QueueFamilyIndices FindQueueFamilies(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanSurface surface) {
		var properties = physicalDevice.GetQueueFamilyProperties();

		var queueFamilyIndices = new QueueFamilyIndices();
		for (uint i = 0; i < properties.Length; i++) {
			var property = properties[i];
			if (property.QueueFlags.HasFlag(QueueFlags.GraphicsBit)) {
				queueFamilyIndices.GraphicsFamily = i;
			}

			var supported = instance.KhrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, surface);
			if (supported) {
				queueFamilyIndices.PresentFamily = i;
			}
			
			if (queueFamilyIndices.IsComplete()) break;
		}
		
		return queueFamilyIndices;
	}

	public SwapchainSupportDetails QuerySwapchainSupport(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanSurface surface) {
		return new SwapchainSupportDetails {
			Capabilities = instance.KhrSurface.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface),
			Formats = instance.KhrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface),
			PresentModes = instance.KhrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface)
		};
	}

	public VulkanDevice CreateLogicalDevice(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanSurface surface) {
		var queueFamilyIndices = FindQueueFamilies(instance, physicalDevice, surface);

		var uniqueQueueFamilies = new[] { queueFamilyIndices.GraphicsFamily!.Value, queueFamilyIndices.PresentFamily!.Value };
		uniqueQueueFamilies = uniqueQueueFamilies.Distinct().ToArray();

		var queueCreateInfos = new DeviceQueueCreateInformation[uniqueQueueFamilies.Length];

		for (var i = 0; i < uniqueQueueFamilies.Length; i++) {
			queueCreateInfos[i] = new DeviceQueueCreateInformation
			{
				QueueFamilyIndex = uniqueQueueFamilies[i],
				QueuePriorities = new[]{ 1.0f }
			};
		}

		var features = new PhysicalDeviceFeatures {
			SamplerAnisotropy = true
		};
		
		var deviceCreateInfo = new DeviceCreateInformation {
			QueueCreateInfos = queueCreateInfos,
			EnabledFeatures = features,
			EnabledExtensions = DeviceExtensions,
			EnabledLayers = ValidationLayers
		};

		return physicalDevice.CreateDevice(deviceCreateInfo);
	}

	private VulkanQueue GetGraphicsQueue(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanDevice device, VulkanSurface surface) {
		var queueFamilyIndices = FindQueueFamilies(instance, physicalDevice, surface);
		return device.GetDeviceQueue(queueFamilyIndices.GraphicsFamily!.Value, 0);
	}

	private VulkanQueue GetPresentQueue(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanDevice device, VulkanSurface surface) {
		var queueFamilyIndices = FindQueueFamilies(instance, physicalDevice, surface);
		return  device.GetDeviceQueue(queueFamilyIndices.PresentFamily!.Value, 0);
	}

	private void RecreateSwapchain(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanDevice device, 
								   VulkanQueue graphicsQueue, VulkanSurface surface, VulkanCommandPool commandPool,
								   VulkanRenderPass renderPass) {
		Vector2D<int> framebufferSize;
		do {
			framebufferSize = window.FramebufferSize;
			window.DoEvents();
		} while (framebufferSize.X == 0 || framebufferSize.Y == 0);

		device.WaitIdle();
		
		CleanupSwapchain();
		
		CreateSwapchain(instance, physicalDevice, device, surface);
		CreateImageViews(device);
		CreateDepthResources(physicalDevice, device, graphicsQueue, commandPool);
		CreateFramebuffers(device, renderPass);
	}

	private void CleanupSwapchain() {
		depth!.Dispose();
		
		foreach (var swapchainFramebuffer in swapchainFramebuffers) {
			swapchainFramebuffer.Dispose();
		}
		foreach (var imageView in swapchainImageViews) {
			imageView.Dispose();
		}
		swapchain!.Dispose();
	}

	private void CreateSwapchain(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanDevice device, VulkanSurface surface) {
		var support = QuerySwapchainSupport(instance, physicalDevice, surface);
		var surfaceFormat = ChooseSwapSurfaceFormat(support.Formats);
		var presentMode = ChooseSwapPresentMode(support.PresentModes);
		var extent = ChooseSwapExtent(support.Capabilities);
		var imageCount = support.Capabilities.MinImageCount + 1;
		if (support.Capabilities.MaxImageCount != 0 && imageCount > support.Capabilities.MaxImageCount) {
			imageCount = support.Capabilities.MaxImageCount;
		}

		var swapchainCreateInfo = new SwapchainCreateInformation {
			Surface = surface,
			MinImageCount = imageCount,
			ImageExtent = extent,
			ImageFormat = surfaceFormat.Format,
			ImageColorSpace = surfaceFormat.ColorSpace,
			PresentMode = presentMode,
			ImageArrayLayers = 1,
			ImageUsage = ImageUsageFlags.ColorAttachmentBit,
			PreTransform = support.Capabilities.CurrentTransform,
			CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
			Clipped = true,
			OldSwapchain = default
		};

		var queueFamilies = FindQueueFamilies(instance, physicalDevice, surface);
		var queueFamilyIndices = new [] { queueFamilies.GraphicsFamily!.Value, queueFamilies.PresentFamily!.Value };
		if (queueFamilies.GraphicsFamily != queueFamilies.PresentFamily) {
			swapchainCreateInfo.ImageSharingMode = SharingMode.Concurrent;
			swapchainCreateInfo.QueueFamilyIndices = queueFamilyIndices;
		}
		else {
			swapchainCreateInfo.ImageSharingMode = SharingMode.Exclusive;
		}

		swapchain = device.KhrSwapchain.CreateSwapchain(swapchainCreateInfo);

		swapchainImages = swapchain.GetImages();

		swapchainFormat = surfaceFormat.Format;
		swapchainExtent = extent;
	}

	private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats) {
		foreach (var surfaceFormat in availableFormats) {
			if (surfaceFormat is { Format: Format.B8G8R8A8Srgb, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr }) {
				return surfaceFormat;
			}
		}
		return availableFormats.First();
	}

	private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availableModes) {
		foreach (var presentMode in availableModes) {
			if (presentMode == PresentModeKHR.MailboxKhr) return presentMode;
		}

		return PresentModeKHR.FifoKhr;
	}

	private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities) {
		if (capabilities.CurrentExtent.Width != uint.MaxValue) {
			return capabilities.CurrentExtent;
		}

		var framebufferSize = window.FramebufferSize;

		Extent2D actualExtent = new () {
			Width = (uint)framebufferSize.X,
			Height = (uint)framebufferSize.Y
		};

		actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
		actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

		return actualExtent;
	}

	private void CreateImageViews(VulkanDevice device) {
		swapchainImageViews = new VulkanImageView[swapchainImages.Length];
		for (var i = 0; i < swapchainImages.Length; i++) {
			swapchainImageViews[i] = device.CreateImageView(new ImageViewCreateInformation {
				Image = swapchainImages[i],
				ViewType = ImageViewType.Type2D,
				Format = swapchainFormat,
				SubresourceRange = new ImageSubresourceRange {
					AspectMask = ImageAspectFlags.ColorBit,
					BaseMipLevel = 0,
					LevelCount = 1,
					BaseArrayLayer = 0,
					LayerCount = 1
				}
			});
		}
	}

	private VulkanRenderPass CreateRenderPass(VulkanPhysicalDevice physicalDevice, VulkanDevice device) {
		return device.CreateRenderPass(new RenderPassCreateInformation {
			Attachments = new[]{new AttachmentDescription {
				Format = swapchainFormat,
				Samples = SampleCountFlags.Count1Bit,
				LoadOp = AttachmentLoadOp.Clear,
				StoreOp = AttachmentStoreOp.Store,
				StencilLoadOp = AttachmentLoadOp.DontCare,
				StencilStoreOp = AttachmentStoreOp.DontCare,
				InitialLayout = ImageLayout.Undefined,
				FinalLayout = ImageLayout.PresentSrcKhr
			}, new AttachmentDescription {
				Format = FindDepthFormat(physicalDevice),
				Samples = SampleCountFlags.Count1Bit,
				LoadOp = AttachmentLoadOp.Clear,
				StoreOp = AttachmentStoreOp.DontCare,
				StencilLoadOp = AttachmentLoadOp.DontCare,
				StencilStoreOp = AttachmentStoreOp.DontCare,
				InitialLayout = ImageLayout.Undefined,
				FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
			}},
			Subpasses = new[] {
				new SubpassDescriptionInformation {
					PipelineBindPoint = PipelineBindPoint.Graphics,
					ColorAttachments = new[] {
						new AttachmentReference {
							Attachment = 0,
							Layout = ImageLayout.ColorAttachmentOptimal
						}
					},
					DepthStencilAttachment = new AttachmentReference {
						Attachment = 1,
						Layout = ImageLayout.DepthStencilAttachmentOptimal
					}
				}
			},
			Dependencies = new[] {
				new SubpassDependency {
					SrcSubpass = Vk.SubpassExternal,
					DstSubpass = 0,
					SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
					SrcAccessMask = AccessFlags.None,
					DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
					DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
				}
			}
		});
	}

	private VulkanPipeline CreateGraphicsPipeline(VulkanDevice device, VulkanRenderPass renderPass, VulkanPipelineLayout pipelineLayout) {
		var vertShaderCode = File.ReadAllBytes("shaders/vert.spv");
		var fragShaderCode = File.ReadAllBytes("shaders/frag.spv");

		using var vertShaderModule = device.CreateShaderModule(vertShaderCode);
		using var fragShaderModule = device.CreateShaderModule(fragShaderCode);
		
		var pipelineInfo = new GraphicsPipelineCreateInformation {
			Stages = new[] { new PipelineShaderStageCreateInformation {
				Stage = ShaderStageFlags.VertexBit,
				Module = vertShaderModule.ShaderModule,
				Name = "main"
			}, new PipelineShaderStageCreateInformation {
				Stage = ShaderStageFlags.FragmentBit,
				Module = fragShaderModule.ShaderModule,
				Name = "main"
			} },
			VertexInputState = new PipelineVertexInputStateCreateInformation {
				VertexAttributeDescriptions = Vertex.GetAttributeDescriptions(),
				VertexBindingDescriptions = new[] { Vertex.GetBindingDescription() }
			},
			InputAssemblyState = new PipelineInputAssemblyStateCreateInformation {
				Topology = PrimitiveTopology.TriangleList,
				PrimitiveRestartEnable = false
			},
			ViewportState = new PipelineViewportStateCreateInformation {
				Scissors = new[] {
					new Rect2D {
						Offset = new Offset2D(0, 0),
						Extent = swapchainExtent
					}
				},
				Viewports = new[]{new Viewport {
					Height = swapchainExtent.Height,
					Width = swapchainExtent.Width,
					X = 0,
					Y = 0,
					MinDepth = 0,
					MaxDepth = 1
				}}
			},
			RasterizationState = new PipelineRasterizationStateCreateInformation {
				DepthClampEnable = false,
				RasterizerDiscardEnable = false,
				PolygonMode = PolygonMode.Fill,
				LineWidth = 1,
				CullMode = CullModeFlags.BackBit,
				FrontFace = FrontFace.Clockwise,
				DepthBiasEnable = false
			},
			MultisampleState = new PipelineMultisampleStateCreateInformation {
				RasterizationSamples = SampleCountFlags.Count1Bit,
				SampleShadingEnable = false
			},
			DepthStencilState = new PipelineDepthStencilStateCreateInformation {
				DepthTestEnable = true,
				DepthWriteEnable = true,
				DepthCompareOp = CompareOp.Less,
				DepthBoundsTestEnable = false,
				StencilTestEnable = false
			},
			ColorBlendState = new PipelineColorBlendStateCreateInformation {
				LogicOpEnable = false,
				LogicOp = LogicOp.Copy,
				Attachments = new[]{new PipelineColorBlendAttachmentState {
					ColorWriteMask = ColorComponentFlags.ABit
									 | ColorComponentFlags.RBit
									 | ColorComponentFlags.GBit
									 | ColorComponentFlags.BBit,
					BlendEnable = false
				}}
			},
			Layout = pipelineLayout,
			RenderPass = renderPass,
			Subpass = 0,
			DynamicState = new PipelineDynamicStateCreateInformation {
				DynamicStates = new[]{DynamicState.Viewport, DynamicState.Scissor}
			}
		};

		return device.CreateGraphicsPipeline(pipelineInfo);
	}

	private void CreateFramebuffers(VulkanDevice device, VulkanRenderPass renderPass) {
		swapchainFramebuffers = new VulkanFramebuffer[swapchainImageViews.Length];

		for (var i = 0; i < swapchainImageViews.Length; i++) {
			var imageView = swapchainImageViews[i].ImageView;
			var framebufferInfo = new FramebufferCreateInformation {
				RenderPass = renderPass,
				Attachments = new[]{imageView, depth!.ImageView},
				Height = swapchainExtent.Height,
				Width = swapchainExtent.Width,
				Layers = 1
			};

			swapchainFramebuffers[i] = device.CreateFramebuffer(framebufferInfo);
		}
	}

	private VulkanCommandPool CreateCommandPool(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanDevice device, VulkanSurface surface) {
		var queueFamilyIndices = FindQueueFamilies(instance, physicalDevice, surface);

		var commandPoolCreateInfo = new CommandPoolCreateInformation {
			QueueFamilyIndex = queueFamilyIndices.GraphicsFamily!.Value,
			Flags = CommandPoolCreateFlags.ResetCommandBufferBit
		};

		return device.CreateCommandPool(commandPoolCreateInfo);
	}

	private void CreateCommandBuffers(VulkanCommandPool commandPool) {
		var commandBuffers = commandPool.AllocateCommandBuffers((uint)renderFrames.Length);

		for (var i = 0; i < renderFrames.Length; i++) {
			renderFrames[i].CommandBuffer = commandBuffers[i];
		}
	}

	private void CreateSyncObjects(VulkanDevice device) {
		foreach (var frame in renderFrames) {
			frame.ImageAvailableSemaphore = device.CreateSemaphore();
			frame.RenderFinishedSemaphore = device.CreateSemaphore();
			frame.InFlightFence = device.CreateFence(FenceCreateFlags.SignaledBit);
		}
	}

	private void RecordCommandBuffer(VulkanCommandBuffer cmd, int index, VulkanRenderPass renderPass, 
									 VulkanPipelineLayout pipelineLayout, VulkanPipeline graphicsPipeline,
									 DescriptorManager manager, VulkanSampler sampler) {
		cmd.Begin();
		
		var colorClear = new ClearValue {
			Color = new ClearColorValue(0, 0, 0, 1)
		};
		var depthClear = new ClearValue {
			DepthStencil = new ClearDepthStencilValue(1, 0)
		};
		var renderPassBegin = new RenderPassBeginInformation {
			RenderPass = renderPass,
			Framebuffer = swapchainFramebuffers[index].Framebuffer,
			RenderArea = new Rect2D(new Offset2D(0, 0), swapchainExtent),
			ClearValues = new []{colorClear, depthClear}
		};
		
		cmd.BeginRenderPass(renderPassBegin, SubpassContents.Inline);
		cmd.BindPipeline(graphicsPipeline);
		
		var viewport = new Viewport {
			Height = swapchainExtent.Height,
			Width = swapchainExtent.Width,
			X = 0,
			Y = 0,
			MaxDepth = 1,
			MinDepth = 0
		};
		var scissor = new Rect2D {
			Offset = new Offset2D(0, 0),
			Extent = swapchainExtent
		};
		
		foreach (var entity in _entities) {
			var set = manager.UpdateDescriptorSet((uint)currentFrame, entity.UniformBuffer!, entity.Texture!.ImageView, sampler);

			cmd.BindVertexBuffer(0, entity.VertexBuffer!);
			cmd.BindIndexBuffer(entity.IndexBuffer!, 0, IndexType.Uint32);
			cmd.SetViewport(0, viewport);
			cmd.SetScissor(0, scissor);
			cmd.BindDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, 0, set);
			cmd.DrawIndexed((uint)entity.Indices.Length);
		}
		
		cmd.EndRenderPass();
		cmd.End();
	}

	private void MainLoop(AppState appState) {
		window.Render += _ => DrawFrame(appState);
		window.Resize += _ => framebufferResized = true;
		window.Run();
		appState.Device.WaitIdle();
	}

	private void DrawFrame(AppState appState) {
		var frame = renderFrames[currentFrame];
		frame.InFlightFence!.Wait();

		uint imageIndex;
		try {
			imageIndex = swapchain!.AcquireNextImage(frame.ImageAvailableSemaphore!);
		}
		catch (ErrorOutOfDateKhrException)
		{
			RecreateSwapchain(appState.Instance, appState.PhysicalDevice, appState.Device, appState.GraphicsQueue,
							  appState.Surface, appState.CommandPool, appState.RenderPass);
			return;
		}
		catch (SuboptimalKhrException)
		{
			RecreateSwapchain(appState.Instance, appState.PhysicalDevice, appState.Device, appState.GraphicsQueue,
							  appState.Surface, appState.CommandPool, appState.RenderPass);
			return;
		}

		frame.InFlightFence.Reset();
		frame.CommandBuffer!.Reset();
		foreach (var entity in _entities) {
			if (!entity.Initialized) {
				entity.Initialize(appState.Device, appState.GraphicsQueue, appState.CommandPool);
			}

			entity.Update(appState.GraphicsQueue, appState.CommandPool, swapchainExtent);
		}

		RecordCommandBuffer(frame.CommandBuffer, (int)imageIndex, appState.RenderPass, appState.PipelineLayout,
							appState.GraphicsPipeline, appState.DescriptorManager, appState.Sampler);

		var buffer = frame.CommandBuffer;
		var signalSemaphores = new[] { frame.RenderFinishedSemaphore!.Semaphore };

		appState.GraphicsQueue.Submit(new SubmitInformation {
			WaitSemaphores = new[]{ frame.ImageAvailableSemaphore!.Semaphore },
			SignalSemaphores = signalSemaphores,
			CommandBuffers = new[]{buffer.CommandBuffer},
			WaitDstStageMask = new[] { PipelineStageFlags.ColorAttachmentOutputBit }
		}, frame.InFlightFence);

		var swapchains = new [] { swapchain.Swapchain };
		var presentInfo = new PresentInformation {
			WaitSemaphores = signalSemaphores,
			Swapchains = swapchains,
			ImageIndices = new[]{imageIndex}
		};
		try {
			appState.Device.KhrSwapchain.QueuePresent(appState.PresentQueue, presentInfo);
		}
		catch (ErrorOutOfDateKhrException) {
			framebufferResized = false;
			RecreateSwapchain(appState.Instance, appState.PhysicalDevice, appState.Device, appState.GraphicsQueue,
							  appState.Surface, appState.CommandPool, appState.RenderPass);
			return;
		}
		catch (SuboptimalKhrException) {
			framebufferResized = false;
			RecreateSwapchain(appState.Instance, appState.PhysicalDevice, appState.Device, appState.GraphicsQueue,
							  appState.Surface, appState.CommandPool, appState.RenderPass);
			return;
		}
		if (framebufferResized) {
			framebufferResized = false;
			RecreateSwapchain(appState.Instance, appState.PhysicalDevice, appState.Device, appState.GraphicsQueue,
							  appState.Surface, appState.CommandPool, appState.RenderPass);
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
		vk.Dispose();
		window.Dispose();
		GC.SuppressFinalize(this);
	}

	~VideoWallApplication() {
		Dispose();
	}

	private static unsafe uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, 
									  DebugUtilsMessageTypeFlagsEXT messageTypes, 
									  DebugUtilsMessengerCallbackDataEXT* pCallbackData, 
									  void* pUserData) {
		var message = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage) ?? string.Empty;
		if (messageTypes.HasFlag(DebugUtilsMessageTypeFlagsEXT.ValidationBitExt)
		    && messageSeverity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt)) {
			throw new ValidationErrorException(message);
		}
		if (messageTypes.HasFlag(DebugUtilsMessageTypeFlagsEXT.ValidationBitExt)
		    || messageTypes.HasFlag(DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt)
		    || messageSeverity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt)
		    || messageSeverity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.WarningBitExt)) {
			throw new Exception(message);
		}

		Console.WriteLine($"Vulkan - " + message);
		return Vk.False;
	}

	private readonly List<EntityData> _entities = new();
	public void AddEntity(IEntity entity) {
		_entities.Add(new EntityData(entity));
	}
}