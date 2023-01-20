using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using SilkNetConvenience;
using SilkNetConvenience.CreateInfo;
using SilkNetConvenience.CreateInfo.EXT;
using SilkNetConvenience.Wrappers;

namespace VideoWall; 

public unsafe class HelloTriangleApplication
{
	private const int MaxFramesInFlight = 2;
	private const int WIDTH = 800;
	private const int HEIGHT = 600;

	private readonly string[] ValidationLayers = new[] {
		"VK_LAYER_KHRONOS_validation"
	};
	public bool EnableValidationLayers = true;

	private readonly string[] DeviceExtensions = new[] {
		KhrSwapchain.ExtensionName
	};

	private readonly Illustrate.Window window;
	private readonly VulkanContext vk;

	private VulkanInstance? instance;
	
	private ExtDebugUtils? debugUtils;
	private DebugUtilsMessengerEXT debugMessenger;

	private KhrSurface? _khrSurface;
	private SurfaceKHR _surface;

	private VulkanPhysicalDevice? _physicalDevice;
	private VulkanDevice? _device;
	private VulkanQueue? _graphicsQueue;
	private VulkanQueue? _presentQueue;

	private KhrSwapchain? _khrSwapchain;
	private SwapchainKHR swapchain;
	private Image[] swapchainImages = Array.Empty<Image>();
	private Format swapchainFormat;
	private Extent2D swapchainExtent;

	private ImageView[] swapchainImageViews = Array.Empty<ImageView>();

	private RenderPass renderPass;
	private VulkanDescriptorSetLayout? descriptorSetLayout;
	private PipelineLayout pipelineLayout;
	private Pipeline graphicsPipeline;

	private Framebuffer[] swapchainFramebuffers = Array.Empty<Framebuffer>();

	private VulkanCommandPool? commandPool;
	private readonly RenderFrame[] renderFrames = new RenderFrame[MaxFramesInFlight];
	private int currentFrame;

	private bool framebufferResized;

	private VulkanBuffer? vertexBuffer;
	private VulkanDeviceMemory? vertexBufferMemory;
	private VulkanBuffer? indexBuffer;
	private VulkanDeviceMemory? indexBufferMemory;

	private readonly Vertex[] vertices = {
		new() { Position = new Vector2D<float>(-0.5f, -0.5f), Color = new Vector3D<float>(1, 0, 0) },
		new() { Position = new Vector2D<float>(0.5f, -0.5f), Color = new Vector3D<float>(0, 1, 0) },
		new() { Position = new Vector2D<float>(0.5f, 0.5f), Color = new Vector3D<float>(0, 0, 1) },
		new() { Position = new Vector2D<float>(-0.5f, 0.5f), Color = new Vector3D<float>(1, 1, 1) }
	};

	private readonly short[] indices = {
		0, 1, 2, 2, 3, 0
	};

	public HelloTriangleApplication() {
		window = new Illustrate.Window("VideoWall", WIDTH, HEIGHT);
		if (window.VkSurface is null)
		{
			throw new Exception("Windowing platform doesn't support Vulkan.");
		}
		
		vk = new VulkanContext();
	}

	public void Run()
	{
		InitVulkan();
		MainLoop();
		CleanUp();
	}

	private void InitVulkan() {
		CreateInstance();
		SetupDebugMessenger();
		CreateSurface();
		PickPhysicalDevice();
		CreateLogicalDevice();
		CreateSwapchain();
		CreateImageViews();
		CreateRenderPass();
		CreateDescriptorSetLayout();
		CreateGraphicsPipeline();
		CreateFramebuffers();
		CreateCommandPool();
		CreateVertexBuffer();
		CreateIndexBuffer();
		CreateCommandBuffers();
		CreateSyncObjects();
	}

	private void CreateDescriptorSetLayout() {
		DescriptorSetLayoutBindingInformation uboLayoutBinding = new() {
			Binding = 0,
			DescriptorType = DescriptorType.UniformBuffer,
			DescriptorCount = 1,
			StageFlags = ShaderStageFlags.VertexBit
		};
		var createInfo = new DescriptorSetLayoutCreateInformation {
			Bindings = new[] { uboLayoutBinding }
		};
		descriptorSetLayout = _device!.CreateDescriptorSetLayout(createInfo);
	}

	private void CreateInstance() {
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
			createInfo.EnabledLayerNames = ValidationLayers;

			PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
			createInfo.DebugUtilsMessengerCreateInfo = debugCreateInfo;
		}
		else {
			createInfo.EnabledLayerNames = Array.Empty<string>();
		}

		instance = vk.CreateInstance(createInfo);
	}

	private string[] GetRequiredExtensions() {
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

	private void SetupDebugMessenger() {
		if (!EnableValidationLayers) return;

		debugUtils = instance!.GetDebugUtilsExtension();
		if (debugUtils == null) return;
		
		var createInfo = new DebugUtilsMessengerCreateInformation();
		PopulateDebugMessengerCreateInfo(ref createInfo);

		debugMessenger = debugUtils!.CreateDebugUtilsMessenger(instance!.Instance, createInfo);
	}

	private static void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInformation createInfo) {
		createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt
		                             | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt;
		createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
		                         | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
		                         | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
		createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
	}

	private void CreateSurface() {
		_khrSurface = instance!.GetKhrSurfaceExtension() ?? throw new NotSupportedException("Could not create a KHR Surface");
		_surface = window.VkSurface!.Create<AllocationCallbacks>(instance.Instance.ToHandle(), null).ToSurface();
	}

	private void PickPhysicalDevice() {
		var devices = instance!.EnumeratePhysicalDevices();
		_physicalDevice = devices.First(IsDeviceSuitable);
	}

	private bool IsDeviceSuitable(VulkanPhysicalDevice device) {
		var queueFamilyIndices = FindQueueFamilies(device);
		var extensionsSupported = CheckDeviceExtensionSupport(device);

		var swapChainAdequate = false;
		if (extensionsSupported) {
			var swapChainSupport = QuerySwapchainSupport(device);
			swapChainAdequate = swapChainSupport.Formats.Any() && swapChainSupport.PresentModes.Any();
		}
		
		return queueFamilyIndices.IsComplete() && extensionsSupported && swapChainAdequate;
	}

	private bool CheckDeviceExtensionSupport(VulkanPhysicalDevice physicalDevice) {
		var properties = physicalDevice.EnumerateExtensionProperties();
		var propertyNames = properties.Select(p => SilkMarshal.PtrToString((nint)p.ExtensionName)).ToList();
		return DeviceExtensions.All(propertyNames.Contains);
	}

	private QueueFamilyIndices FindQueueFamilies(VulkanPhysicalDevice physicalDevice) {
		var properties = physicalDevice.GetQueueFamilyProperties();

		var queueFamilyIndices = new QueueFamilyIndices();
		for (uint i = 0; i < properties.Length; i++) {
			var property = properties[i];
			if (property.QueueFlags.HasFlag(QueueFlags.GraphicsBit)) {
				queueFamilyIndices.GraphicsFamily = i;
			}

			_khrSurface!.GetPhysicalDeviceSurfaceSupport(physicalDevice.PhysicalDevice, i, _surface, out var supported);
			if (supported) {
				queueFamilyIndices.PresentFamily = i;
			}
			
			if (queueFamilyIndices.IsComplete()) break;
		}
		
		return queueFamilyIndices;
	}

	public SwapChainSupportDetails QuerySwapchainSupport(VulkanPhysicalDevice physicalDevice) {
		var details = new SwapChainSupportDetails();

		_khrSurface!.GetPhysicalDeviceSurfaceCapabilities(physicalDevice.PhysicalDevice, _surface, out details.Capabilities);

		uint formatCount = 0;
		_khrSurface!.GetPhysicalDeviceSurfaceFormats(physicalDevice.PhysicalDevice, _surface, ref formatCount, null);
		details.Formats = new SurfaceFormatKHR[formatCount];
		fixed (SurfaceFormatKHR* formatsPointer = details.Formats) {
			_khrSurface!.GetPhysicalDeviceSurfaceFormats(physicalDevice.PhysicalDevice, _surface, ref formatCount, formatsPointer);
		}
		
		uint presentModesCount = 0;
		_khrSurface!.GetPhysicalDeviceSurfacePresentModes(physicalDevice.PhysicalDevice, _surface, ref presentModesCount, null);
		details.PresentModes = new PresentModeKHR[presentModesCount];
		fixed (PresentModeKHR* presentModesPointer = details.PresentModes) {
			_khrSurface!.GetPhysicalDeviceSurfacePresentModes(physicalDevice.PhysicalDevice, _surface, ref presentModesCount, presentModesPointer);
		}

		return details;
	}

	public void CreateLogicalDevice() {
		var queueFamilyIndices = FindQueueFamilies(_physicalDevice!);

		var uniqueQueueFamilies = new[] { queueFamilyIndices.GraphicsFamily!.Value, queueFamilyIndices.PresentFamily!.Value };
		uniqueQueueFamilies = uniqueQueueFamilies.Distinct().ToArray();

		using var mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
		var queueCreateInfos = new DeviceQueueCreateInformation[uniqueQueueFamilies.Length];

		for (var i = 0; i < uniqueQueueFamilies.Length; i++) {
			queueCreateInfos[i] = new DeviceQueueCreateInformation
			{
				QueueFamilyIndex = uniqueQueueFamilies[i],
				QueuePriorities = new[]{ 1.0f }
			};
		}

		var features = new PhysicalDeviceFeatures();
		
		var deviceCreateInfo = new DeviceCreateInformation {
			QueueCreateInfos = queueCreateInfos,
			EnabledFeatures = features,
			EnabledExtensionNames = DeviceExtensions,
			EnabledLayerNames = ValidationLayers
		};

		_device = _physicalDevice!.CreateDevice(deviceCreateInfo);

		_graphicsQueue = _device.GetDeviceQueue(queueFamilyIndices.GraphicsFamily.Value, 0);
		_presentQueue = _device.GetDeviceQueue(queueFamilyIndices.PresentFamily.Value, 0);
	}

	private void RecreateSwapChain() {
		Vector2D<int> framebufferSize;
		do {
			framebufferSize = window.FramebufferSize;
			window.DoEvents();
		} while (framebufferSize.X == 0 || framebufferSize.Y == 0);

		_device!.WaitIdle();
		
		CleanupSwapchain();
		
		CreateSwapchain();
		CreateImageViews();
		CreateFramebuffers();
	}

	private void CleanupSwapchain() {
		foreach (var swapchainFramebuffer in swapchainFramebuffers) {
			vk.Vk.DestroyFramebuffer(_device!.Device, swapchainFramebuffer);
		}
		foreach (var imageView in swapchainImageViews) {
			vk.Vk.DestroyImageView(_device!.Device, imageView);
		}
		_khrSwapchain!.DestroySwapchain(_device!.Device, swapchain);
	}

	private void CreateSwapchain() {
		var support = QuerySwapchainSupport(_physicalDevice!);
		var surfaceFormat = ChooseSwapSurfaceFormat(support.Formats);
		var presentMode = ChooseSwapPresentMode(support.PresentModes);
		var extent = ChooseSwapExtent(support.Capabilities);
		var imageCount = support.Capabilities.MinImageCount + 1;
		if (support.Capabilities.MaxImageCount != 0 && imageCount > support.Capabilities.MaxImageCount) {
			imageCount = support.Capabilities.MaxImageCount;
		}

		var swapchainCreateInfo = new SwapchainCreateInfoKHR {
			SType = StructureType.SwapchainCreateInfoKhr,
			Surface = _surface,
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

		var queueFamilies = FindQueueFamilies(_physicalDevice!);
		var queueFamilyIndices = stackalloc [] { queueFamilies.GraphicsFamily!.Value, queueFamilies.PresentFamily!.Value };
		if (queueFamilies.GraphicsFamily != queueFamilies.PresentFamily) {
			swapchainCreateInfo.ImageSharingMode = SharingMode.Concurrent;
			swapchainCreateInfo.QueueFamilyIndexCount = 2;
			swapchainCreateInfo.PQueueFamilyIndices = queueFamilyIndices;
		}
		else {
			swapchainCreateInfo.ImageSharingMode = SharingMode.Exclusive;
			swapchainCreateInfo.QueueFamilyIndexCount = 0;
			swapchainCreateInfo.PQueueFamilyIndices = null;
		}

		_khrSwapchain = _device!.GetKhrSwapchainExtension() ?? throw new NotSupportedException("VK_KHR_swapchain extension not found.");

		if (_khrSwapchain!.CreateSwapchain(_device!.Device, swapchainCreateInfo, null, out swapchain) != Result.Success) {
			throw new Exception("Failed to create swapchain");
		}

		swapchainImages = Helpers.GetArray((ref uint length, Image* data) =>
			_khrSwapchain.GetSwapchainImages(_device!.Device, swapchain, ref length, data));

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

	private void CreateImageViews() {
		swapchainImageViews = new ImageView[swapchainImages.Length];
		for (var i = 0; i < swapchainImages.Length; i++) {
			var imageViewCreateInfo = new ImageViewCreateInformation {
				Image = swapchainImages[i],
				ViewType = ImageViewType.Type2D,
				Format = swapchainFormat,
				Components = new ComponentMapping {
					A = ComponentSwizzle.Identity,
					R = ComponentSwizzle.Identity,
					G = ComponentSwizzle.Identity,
					B = ComponentSwizzle.Identity
				},
				SubresourceRange = new ImageSubresourceRange {
					AspectMask = ImageAspectFlags.ColorBit,
					BaseMipLevel = 0,
					LayerCount = 1,
					LevelCount = 1,
					BaseArrayLayer = 0
				}
			};

			swapchainImageViews[i] = vk.Vk.CreateImageView(_device!.Device, imageViewCreateInfo);
		}
	}

	private void CreateRenderPass() {
		var colorAttachment = new AttachmentDescription {
			Format = swapchainFormat,
			Samples = SampleCountFlags.Count1Bit,
			LoadOp = AttachmentLoadOp.Clear,
			StoreOp = AttachmentStoreOp.Store,
			StencilLoadOp = AttachmentLoadOp.DontCare,
			StencilStoreOp = AttachmentStoreOp.DontCare,
			InitialLayout = ImageLayout.Undefined,
			FinalLayout = ImageLayout.PresentSrcKhr
		};

		var colorAttachmentRef = new AttachmentReference {
			Attachment = 0,
			Layout = ImageLayout.ColorAttachmentOptimal
		};
		
		var subpass = new SubpassDescription {
			PipelineBindPoint = PipelineBindPoint.Graphics,
			ColorAttachmentCount = 1,
			PColorAttachments = &colorAttachmentRef
		};

		var subpassDependency = new SubpassDependency {
			SrcSubpass = Vk.SubpassExternal,
			DstSubpass = 0,
			SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
			SrcAccessMask = AccessFlags.None,
			DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
			DstAccessMask = AccessFlags.ColorAttachmentWriteBit
		};
		
		var createInfo = new RenderPassCreateInformation {
			Attachments = new[]{colorAttachment},
			Subpasses = new[]{subpass},
			Dependencies = new[]{subpassDependency}
		};

		renderPass = vk.Vk.CreateRenderPass(_device!.Device, createInfo);
	}

	private void CreateGraphicsPipeline() {
		var vertShaderCode = File.ReadAllBytes("shaders/vert.spv");
		var fragShaderCode = File.ReadAllBytes("shaders/frag.spv");

		var vertShaderModule = CreateShaderModule(vertShaderCode);
		var fragShaderModule = CreateShaderModule(fragShaderCode);

		var vertShaderStageInfo = new PipelineShaderStageCreateInfo {
			SType = StructureType.PipelineShaderStageCreateInfo,
			Stage = ShaderStageFlags.VertexBit,
			Module = vertShaderModule,
			PName = (byte*)SilkMarshal.StringToPtr("main")
		};

		var fragShaderStageInfo = new PipelineShaderStageCreateInfo {
			SType = StructureType.PipelineShaderStageCreateInfo,
			Stage = ShaderStageFlags.FragmentBit,
			Module = fragShaderModule,
			PName = (byte*)SilkMarshal.StringToPtr("main")
		};

		var shaderStages = new[] { vertShaderStageInfo, fragShaderStageInfo };

		var bindings = Vertex.GetBindingDescription();
		var attributes = Vertex.GetAttributeDescriptions();

		fixed (VertexInputAttributeDescription* attributesPointer = attributes) {
			var vertexInputInfo = new PipelineVertexInputStateCreateInfo {
				SType = StructureType.PipelineVertexInputStateCreateInfo,
				VertexAttributeDescriptionCount = (uint)attributes.Length,
				PVertexAttributeDescriptions = attributesPointer,
				VertexBindingDescriptionCount = 1,
				PVertexBindingDescriptions = &bindings
			};

			var inputAssembly = new PipelineInputAssemblyStateCreateInfo {
				SType = StructureType.PipelineInputAssemblyStateCreateInfo,
				Topology = PrimitiveTopology.TriangleList,
				PrimitiveRestartEnable = false
			};

			var viewport = new Viewport {
				Height = swapchainExtent.Height,
				Width = swapchainExtent.Width,
				X = 0,
				Y = 0,
				MinDepth = 0,
				MaxDepth = 1
			};

			var scissor = new Rect2D {
				Offset = new Offset2D(0, 0),
				Extent = swapchainExtent
			};

			var viewportState = new PipelineViewportStateCreateInfo {
				SType = StructureType.PipelineViewportStateCreateInfo,
				ScissorCount = 1,
				PScissors = &scissor,
				ViewportCount = 1,
				PViewports = &viewport
			};

			var rasterizer = new PipelineRasterizationStateCreateInfo {
				SType = StructureType.PipelineRasterizationStateCreateInfo,
				DepthClampEnable = false,
				RasterizerDiscardEnable = false,
				PolygonMode = PolygonMode.Fill,
				LineWidth = 1,
				CullMode = CullModeFlags.BackBit,
				FrontFace = FrontFace.Clockwise,
				DepthBiasEnable = false
			};

			var colorBlendAttachment = new PipelineColorBlendAttachmentState {
				ColorWriteMask = ColorComponentFlags.ABit
				                 | ColorComponentFlags.RBit
				                 | ColorComponentFlags.GBit
				                 | ColorComponentFlags.BBit,
				BlendEnable = false
			};

			var colorBlending = new PipelineColorBlendStateCreateInfo {
				SType = StructureType.PipelineColorBlendStateCreateInfo,
				LogicOpEnable = false,
				LogicOp = LogicOp.Copy,
				AttachmentCount = 1,
				PAttachments = &colorBlendAttachment,
			};

			var sampler = new PipelineMultisampleStateCreateInfo {
				SType = StructureType.PipelineMultisampleStateCreateInfo,
				RasterizationSamples = SampleCountFlags.Count1Bit,
				SampleShadingEnable = false
			};

			var pipelineLayoutInfo = new PipelineLayoutCreateInformation {
				SetLayouts = new[]{descriptorSetLayout!.DescriptorSetLayout}
			};

			pipelineLayout = vk.Vk.CreatePipelineLayout(_device!.Device, pipelineLayoutInfo);

			var dynamicStates = stackalloc[] { DynamicState.Viewport, DynamicState.Scissor };
			var pipelineDynamicStateInfo = new PipelineDynamicStateCreateInfo {
				SType = StructureType.PipelineDynamicStateCreateInfo,
				DynamicStateCount = 2,
				PDynamicStates = dynamicStates
			};
			fixed (PipelineShaderStageCreateInfo* shaderStagesPointer = shaderStages) {
				var pipelineInfo = new GraphicsPipelineCreateInfo {
					SType = StructureType.GraphicsPipelineCreateInfo,
					StageCount = 2,
					PStages = shaderStagesPointer,
					PVertexInputState = &vertexInputInfo,
					PInputAssemblyState = &inputAssembly,
					PViewportState = &viewportState,
					PRasterizationState = &rasterizer,
					PMultisampleState = &sampler,
					PDepthStencilState = null,
					PColorBlendState = &colorBlending,
					Layout = pipelineLayout,
					RenderPass = renderPass,
					Subpass = 0,
					PDynamicState = &pipelineDynamicStateInfo
				};

				if (vk.Vk.CreateGraphicsPipelines(_device!.Device, default, 1, &pipelineInfo, null, out graphicsPipeline) !=
				    Result.Success) {
					throw new Exception("Failed to create graphics pipelines");
				}
			}
		}

		SilkMarshal.Free((nint)vertShaderStageInfo.PName);
		SilkMarshal.Free((nint)fragShaderStageInfo.PName);
		vk.Vk.DestroyShaderModule(_device!.Device, vertShaderModule, null);
		vk.Vk.DestroyShaderModule(_device!.Device, fragShaderModule, null);
	}

	private ShaderModule CreateShaderModule(byte[] code) {
		fixed (byte* codePointer = code) {
			var createInfo = new ShaderModuleCreateInfo {
				SType = StructureType.ShaderModuleCreateInfo,
				CodeSize = (uint)code.Length,
				PCode = (uint*)codePointer
			};
			if (vk.Vk.CreateShaderModule(_device!.Device, createInfo, null, out var shaderModule) != Result.Success) {
				throw new Exception("Could not create a shader module");
			}
			return shaderModule;
		}
	}

	private void CreateFramebuffers() {
		swapchainFramebuffers = new Framebuffer[swapchainImageViews.Length];

		for (var i = 0; i < swapchainImageViews.Length; i++) {
			var imageView = swapchainImageViews[i];
			var framebufferInfo = new FramebufferCreateInfo {
				SType = StructureType.FramebufferCreateInfo,
				RenderPass = renderPass,
				AttachmentCount = 1,
				PAttachments = &imageView,
				Height = swapchainExtent.Height,
				Width = swapchainExtent.Width,
				Layers = 1
			};

			if (vk.Vk.CreateFramebuffer(_device!.Device, framebufferInfo, null, out swapchainFramebuffers[i]) != Result.Success) {
				throw new Exception("Failed to create framebuffer");
			}
		}
	}

	private void CreateCommandPool() {
		var queueFamilyIndices = FindQueueFamilies(_physicalDevice!);

		var commandPoolCreateInfo = new CommandPoolCreateInformation {
			QueueFamilyIndex = queueFamilyIndices.GraphicsFamily!.Value,
			Flags = CommandPoolCreateFlags.ResetCommandBufferBit
		};

		commandPool = _device!.CreateCommandPool(commandPoolCreateInfo);
	}

	private void CreateVertexBuffer() {
		var bufferSize = (uint)(Unsafe.SizeOf<Vertex>() * vertices.Length);
		var (stagingBuffer, stagingBufferMemory) = CreateBuffer(bufferSize, BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
		
		using (stagingBuffer)
		using (stagingBufferMemory) {
			var data = stagingBufferMemory.MapMemory<Vertex>();
			vertices.AsSpan().CopyTo(data);
			stagingBufferMemory.UnmapMemory();
		
			(vertexBuffer, vertexBufferMemory) = CreateBuffer(bufferSize, 
				BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, 
				MemoryPropertyFlags.DeviceLocalBit);

			CopyBuffer(stagingBuffer, vertexBuffer, bufferSize);
		}
	}

	private void CreateIndexBuffer() {
		var bufferSize =  sizeof(short) * (uint)indices.Length;

		var (stagingBuffer, stagingMemory) = CreateBuffer(bufferSize, BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit);

		using (stagingBuffer)
		using (stagingMemory) {
			var data = stagingMemory.MapMemory<short>();
			indices.AsSpan().CopyTo(data);
			stagingMemory.UnmapMemory();

			(indexBuffer, indexBufferMemory) = CreateBuffer(bufferSize,
				BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
				MemoryPropertyFlags.DeviceLocalBit);

			CopyBuffer(stagingBuffer, indexBuffer, bufferSize);
		}
	}

	private void CopyBuffer(VulkanBuffer source, VulkanBuffer destination, uint size) {
		using var commandBuffer = commandPool!.AllocateCommandBuffer(CommandBufferLevel.Primary);
		commandBuffer.Begin(CommandBufferUsageFlags.OneTimeSubmitBit);
		commandBuffer.CopyBuffer(source, destination, size);
		commandBuffer.End();

		_graphicsQueue!.Submit(commandBuffer);
		_graphicsQueue!.WaitIdle();
	}

	private (VulkanBuffer buffer, VulkanDeviceMemory memory) CreateBuffer(uint size, BufferUsageFlags usage, MemoryPropertyFlags properties) {
		var bufferCreateInfo = new BufferCreateInformation {
			Size = size,
			Usage = usage,
			SharingMode = SharingMode.Exclusive
		};
		var buffer = _device!.CreateBuffer(bufferCreateInfo);

		var memoryRequirements = buffer.GetMemoryRequirements();

		var allocInfo = new MemoryAllocateInformation {
			AllocationSize = memoryRequirements.Size,
			MemoryTypeIndex = FindMemoryType(memoryRequirements.MemoryTypeBits, properties)
		};
		var bufferMemory = _device.AllocateMemory(allocInfo);

		buffer.BindMemory(bufferMemory);
		
		return (buffer, bufferMemory);
	}

	private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties) {
		var memoryProperties = _physicalDevice!.GetMemoryProperties();

		for (var i = 0; i < memoryProperties.MemoryTypeCount; i++) {
			var memType = memoryProperties.MemoryTypes[i];
			if ((typeFilter & (1 << i)) != 0 && (memType.PropertyFlags & properties) == properties) {
				return (uint)i;
			}
		}

		throw new Exception("Failed to find a suitable memory location");
	}

	private void CreateCommandBuffers() {
		var commandBuffers = commandPool!.AllocateCommandBuffers((uint)renderFrames.Length, CommandBufferLevel.Primary);

		for (var i = 0; i < renderFrames.Length; i++) {
			renderFrames[i] = new RenderFrame {
				CommandBuffer = commandBuffers[i]
			};
		}
	}

	private void CreateSyncObjects() {
		var semaphoreInfo = new SemaphoreCreateInfo {
			SType = StructureType.SemaphoreCreateInfo
		};
		var fenceInfo = new FenceCreateInfo {
			SType = StructureType.FenceCreateInfo,
			Flags = FenceCreateFlags.SignaledBit
		};
		foreach (var frame in renderFrames) {
			if (vk.Vk.CreateSemaphore(_device!.Device, semaphoreInfo, null, out frame.ImageAvailableSemaphore) != Result.Success
			    || vk.Vk.CreateSemaphore(_device!.Device, semaphoreInfo, null, out frame.RenderFinishedSemaphore) != Result.Success
			    || vk.Vk.CreateFence(_device!.Device, fenceInfo, null, out frame.InFlightFence) != Result.Success) {
				throw new Exception("Failed to create sync objects");
			}
		}
	}

	private void RecordCommandBuffer(VulkanCommandBuffer buffer, int index) {
		buffer.Begin();
		
		var clearValue = new ClearValue {
			Color = new ClearColorValue(0, 0, 0, 1)
		};
		var renderPassBegin = new RenderPassBeginInformation {
			RenderPass = renderPass,
			Framebuffer = swapchainFramebuffers[index],
			RenderArea = new Rect2D(new Offset2D(0, 0), swapchainExtent),
			ClearValues = new []{clearValue}
		};
		
		buffer.BeginRenderPass(renderPassBegin, SubpassContents.Inline);
		buffer.BindPipeline(PipelineBindPoint.Graphics, graphicsPipeline);
		
		buffer.BindVertexBuffer(0, vertexBuffer!.Buffer);
		buffer.BindIndexBuffer(indexBuffer!, 0, IndexType.Uint16);

		var viewport = new Viewport {
			Height = swapchainExtent.Height,
			Width = swapchainExtent.Width,
			X = 0,
			Y = 0,
			MaxDepth = 1,
			MinDepth = 0
		};
		buffer.SetViewport(0, viewport);

		var scissor = new Rect2D {
			Offset = new Offset2D(0, 0),
			Extent = swapchainExtent
		};
		buffer.SetScissor(0, scissor);

		buffer.DrawIndexed((uint)indices.Length);

		buffer.EndRenderPass();

		buffer.End();
	}

	private void MainLoop() {
		window.Render += DrawFrame;
		window.Resize += _ => framebufferResized = true;
		window.Run();
		_device!.WaitIdle();
	}

	private void DrawFrame(double dt) {
		var frame = renderFrames[currentFrame];
		vk.Vk.WaitForFences(_device!.Device, 1, frame.InFlightFence, true, int.MaxValue);

		uint imageIndex = 0;
		var acquireResult = _khrSwapchain!.AcquireNextImage(_device!.Device, swapchain, int.MaxValue, frame.ImageAvailableSemaphore, default, ref imageIndex);
		if (acquireResult == Result.ErrorOutOfDateKhr) {
			RecreateSwapChain();
			return;
		}
		if (acquireResult != Result.Success && acquireResult != Result.SuboptimalKhr) {
			throw new Exception("failed to acquire next image");
		}
		
		vk.Vk.ResetFences(_device!.Device, 1, frame.InFlightFence);

		frame.CommandBuffer!.Reset();
		RecordCommandBuffer(frame.CommandBuffer, (int)imageIndex);

		var buffer = frame.CommandBuffer;
		var waitSemaphores = stackalloc[] { frame.ImageAvailableSemaphore };
		var pipelineStageFlags = stackalloc [] { PipelineStageFlags.ColorAttachmentOutputBit };
		var signalSemaphores = stackalloc[] { frame.RenderFinishedSemaphore };
		var commandBuffer = buffer.CommandBuffer;
		var submitInfo = new SubmitInfo {
			SType = StructureType.SubmitInfo,
			WaitSemaphoreCount = 1,
			PWaitSemaphores = waitSemaphores,
			PWaitDstStageMask = pipelineStageFlags,
			CommandBufferCount = 1,
			PCommandBuffers = &commandBuffer,
			SignalSemaphoreCount = 1,
			PSignalSemaphores = signalSemaphores
		};

		if (vk.Vk.QueueSubmit(_graphicsQueue!.Queue, 1, submitInfo, frame.InFlightFence) != Result.Success) {
			throw new Exception("Failed to submit queue");
		}

		var swapchains = stackalloc [] { swapchain };
		var presentInfo = new PresentInfoKHR {
			SType = StructureType.PresentInfoKhr,
			WaitSemaphoreCount = 1,
			PWaitSemaphores = signalSemaphores,
			SwapchainCount = 1,
			PSwapchains = swapchains,
			PImageIndices = &imageIndex
		};
		var presentResult = _khrSwapchain.QueuePresent(_presentQueue!.Queue, presentInfo);
		if (presentResult is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr || framebufferResized) {
			framebufferResized = false;
			RecreateSwapChain();
			return;
		}
		if (presentResult != Result.Success) {
			throw new Exception("Failed to present queue");
		}
		currentFrame = (currentFrame + 1) % MaxFramesInFlight;
	}

	private void CleanUp() {
		foreach (var frame in renderFrames) {
			vk.Vk.DestroySemaphore(_device!.Device, frame.ImageAvailableSemaphore, null);
			vk.Vk.DestroySemaphore(_device!.Device, frame.RenderFinishedSemaphore, null);
			vk.Vk.DestroyFence(_device!.Device, frame.InFlightFence, null);
		}
		commandPool!.Dispose();
		
		vk.Vk.DestroyPipeline(_device!.Device, graphicsPipeline);
		vk.Vk.DestroyPipelineLayout(_device!.Device, pipelineLayout);
		vk.Vk.DestroyRenderPass(_device!.Device, renderPass, null);
		
		CleanupSwapchain();
		descriptorSetLayout!.Dispose();

		indexBuffer!.Dispose();
		indexBufferMemory!.Dispose();
		
		vertexBuffer!.Dispose();
		vertexBufferMemory!.Dispose();
		
		_device!.Dispose();
		if (EnableValidationLayers) {
			debugUtils!.DestroyDebugUtilsMessenger(instance!.Instance, debugMessenger, null);
		}

		_khrSurface!.DestroySurface(instance!.Instance, _surface, null);
		vk.Vk.DestroyInstance(instance!.Instance);
		vk.Dispose();
		window.Dispose();
	}
	
	private static uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, 
								DebugUtilsMessageTypeFlagsEXT messageTypes, 
								DebugUtilsMessengerCallbackDataEXT* pCallbackData, 
								void* pUserData) {
		var severity = messageSeverity switch {
			DebugUtilsMessageSeverityFlagsEXT.None => "None",
			DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt => "Error",
			DebugUtilsMessageSeverityFlagsEXT.WarningBitExt => "Warning",
			DebugUtilsMessageSeverityFlagsEXT.InfoBitExt => "Info",
			DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt => "Verbose",
			_ => throw new ArgumentOutOfRangeException(nameof(messageSeverity), messageSeverity, null)
		};
		var type = messageTypes switch {
			DebugUtilsMessageTypeFlagsEXT.None => "None",
			DebugUtilsMessageTypeFlagsEXT.GeneralBitExt => "General",
			DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt => "Performance",
			DebugUtilsMessageTypeFlagsEXT.ValidationBitExt => "Validation",
			_ => throw new ArgumentOutOfRangeException(nameof(messageTypes), messageTypes, null)
		};
		Console.WriteLine($"Vulkan {severity} {type}: " + Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));
		return Vk.False;
	}
}