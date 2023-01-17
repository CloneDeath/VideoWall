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
using Silk.NET.Windowing;
using Buffer = Silk.NET.Vulkan.Buffer;

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

	private IWindow? window;
	private Vk? vk;

	private Instance instance;
	
	private ExtDebugUtils? debugUtils;
	private DebugUtilsMessengerEXT debugMessenger;

	private KhrSurface? _khrSurface;
	private SurfaceKHR _surface;

	private PhysicalDevice _physicalDevice;
	private Device _device;
	private Queue _graphicsQueue;
	private Queue _presentQueue;

	private KhrSwapchain? _khrSwapchain;
	private SwapchainKHR swapchain;
	private Image[] swapchainImages = Array.Empty<Image>();
	private Format swapchainFormat;
	private Extent2D swapchainExtent;

	private ImageView[] swapchainImageViews = Array.Empty<ImageView>();

	private RenderPass renderPass;
	private PipelineLayout pipelineLayout;
	private Pipeline graphicsPipeline;

	private Framebuffer[] swapchainFramebuffers = Array.Empty<Framebuffer>();

	private CommandPool commandPool;
	private readonly RenderFrame[] renderFrames = new RenderFrame[MaxFramesInFlight];
	private int currentFrame;

	private bool framebufferResized;

	private Buffer vertexBuffer;
	private DeviceMemory vertexBufferMemory;

	private readonly Vertex[] vertices = {
		new() { Position = new Vector2D<float>(0, -0.5f), Color = new Vector3D<float>(1, 1, 1) },
		new() { Position = new Vector2D<float>(0.5f, 0.5f), Color = new Vector3D<float>(0, 1, 0) },
		new() { Position = new Vector2D<float>(-0.5f, 0.5f), Color = new Vector3D<float>(0, 0, 1) }
	};

	public void Run()
	{
		InitWindow();
		InitVulkan();
		MainLoop();
		CleanUp();
	}

	private void InitWindow()
	{
		var options = WindowOptions.DefaultVulkan with {
			Size = new Vector2D<int>(WIDTH, HEIGHT),
			Title = "Vulkan"
		};

		window = Window.Create(options);
		window.Initialize();
		if (window.VkSurface is null)
		{
			throw new Exception("Windowing platform doesn't support Vulkan.");
		}
	}

	private void InitVulkan() {
		CreateInstance();
		SetupDebugMessenger();
		CreateSurface();
		PickPhysicalDevice();
		CreateLogicalDevice();
		CreateSwapChain();
		CreateImageViews();
		CreateRenderPass();
		CreateGraphicsPipeline();
		CreateFramebuffers();
		CreateCommandPool();
		CreateVertexBuffer();
		CreateCommandBuffers();
		CreateSyncObjects();
	}

	private void CreateInstance() {
		vk = Vk.GetApi();

		if (EnableValidationLayers && !CheckValidationLayerSupport()) {
			throw new Exception("Validation layers not found");
		}
		var appInfo = new ApplicationInfo {
			SType = StructureType.ApplicationInfo,
			PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Hello Triangle"),
			ApplicationVersion = Vk.MakeVersion(1, 0),
			PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
			EngineVersion = Vk.MakeVersion(1, 0),
			ApiVersion = Vk.MakeVersion(1, 0)
		};

		var extensions = GetRequiredExtensions();
		var createInfo = new InstanceCreateInfo {
			SType = StructureType.InstanceCreateInfo,
			PApplicationInfo = &appInfo,
			EnabledExtensionCount = (uint)extensions.Length,
			PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions)
		};
		
		DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new ();
		if (EnableValidationLayers) {
			createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
			createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);

			PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
			createInfo.PNext = &debugCreateInfo;
		}
		else {
			createInfo.EnabledLayerCount = 0;
		}

		if (vk.CreateInstance(createInfo, null, out instance) != Result.Success) {
			throw new Exception("Failed to create Vulkan Instance");
		}
		
		Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
		Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
		SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
		if (EnableValidationLayers) {
			SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
		}
	}
	
	private string[] GetRequiredExtensions() {
		var glfwExtensions = window!.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
		var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);

		return EnableValidationLayers 
			? extensions.Append(ExtDebugUtils.ExtensionName).ToArray() 
			: extensions;
	}

	private bool CheckValidationLayerSupport() {
		var availableLayers = Helpers.GetArray((ref uint length, LayerProperties* data) =>
			vk!.EnumerateInstanceLayerProperties(ref length, data));
		var availableLayerNames = availableLayers.Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName)).ToHashSet();
		return ValidationLayers.All(availableLayerNames.Contains);
	}

	private void SetupDebugMessenger() {
		if (!EnableValidationLayers) return;

		if (!vk!.TryGetInstanceExtension(instance, out debugUtils)) return;
		
		var createInfo = new DebugUtilsMessengerCreateInfoEXT();
		PopulateDebugMessengerCreateInfo(ref createInfo);

		if (debugUtils!.CreateDebugUtilsMessenger(instance, createInfo, null, out debugMessenger) != Result.Success) {
			throw new Exception("Could not hook in debug messenger");
		}
	}

	private static void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo) {
		createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
		createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt
		                             | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt;
		createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
		                         | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
		                         | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
		createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
	}

	private void CreateSurface() {
		if (!vk!.TryGetInstanceExtension(instance, out _khrSurface)) {
			throw new NotSupportedException("Could not create a KHR Surface");
		}
		
		_surface = window!.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();
	}

	private void PickPhysicalDevice() {
		var physicalDeviceCount = 0u;
		vk!.EnumeratePhysicalDevices(instance, ref physicalDeviceCount, null);

		if (physicalDeviceCount == 0) throw new Exception("Found 0 devices with Vulkan support");
		
		var devices = new PhysicalDevice[physicalDeviceCount];
		fixed (PhysicalDevice* devicesPtr = devices)
		{
			vk!.EnumeratePhysicalDevices(instance, ref physicalDeviceCount, devicesPtr);
		}

		_physicalDevice = devices.First(IsDeviceSuitable);
	}

	private bool IsDeviceSuitable(PhysicalDevice device) {
		var indices = FindQueueFamilies(device);
		var extensionsSupported = CheckDeviceExtensionSupport(device);

		var swapChainAdequate = false;
		if (extensionsSupported) {
			var swapChainSupport = QuerySwapChainSupport(device);
			swapChainAdequate = swapChainSupport.Formats.Any() && swapChainSupport.PresentModes.Any();
		}
		
		return indices.IsComplete() && extensionsSupported && swapChainAdequate;
	}

	private bool CheckDeviceExtensionSupport(PhysicalDevice device) {
		uint extensionCount = 0;
		vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, null);

		var properties = new ExtensionProperties[extensionCount];
		fixed (ExtensionProperties* propertiesPointer = properties) {
			vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionCount, propertiesPointer);
		}

		var propertyNames = properties.Select(p => SilkMarshal.PtrToString((nint)p.ExtensionName)).ToList();
		return DeviceExtensions.All(propertyNames.Contains);
	}

	private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device) {
		uint queueFamilyCount = 0;
		vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

		var properties = new QueueFamilyProperties[queueFamilyCount];
		fixed (QueueFamilyProperties* propertiesPointer = properties) {
			vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, propertiesPointer);
		}

		var queueFamilyIndices = new QueueFamilyIndices();
		for (uint i = 0; i < properties.Length; i++) {
			var property = properties[i];
			if (property.QueueFlags.HasFlag(QueueFlags.GraphicsBit)) {
				queueFamilyIndices.GraphicsFamily = i;
			}

			_khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, _surface, out var supported);
			if (supported) {
				queueFamilyIndices.PresentFamily = i;
			}
			
			if (queueFamilyIndices.IsComplete()) break;
		}
		
		return queueFamilyIndices;
	}

	public SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice physicalDevice) {
		var details = new SwapChainSupportDetails();

		_khrSurface!.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, _surface, out details.Capabilities);

		uint formatCount = 0;
		_khrSurface!.GetPhysicalDeviceSurfaceFormats(physicalDevice, _surface, ref formatCount, null);
		details.Formats = new SurfaceFormatKHR[formatCount];
		fixed (SurfaceFormatKHR* formatsPointer = details.Formats) {
			_khrSurface!.GetPhysicalDeviceSurfaceFormats(physicalDevice, _surface, ref formatCount, formatsPointer);
		}
		
		uint presentModesCount = 0;
		_khrSurface!.GetPhysicalDeviceSurfacePresentModes(physicalDevice, _surface, ref presentModesCount, null);
		details.PresentModes = new PresentModeKHR[presentModesCount];
		fixed (PresentModeKHR* presentModesPointer = details.PresentModes) {
			_khrSurface!.GetPhysicalDeviceSurfacePresentModes(physicalDevice, _surface, ref presentModesCount, presentModesPointer);
		}

		return details;
	}

	public void CreateLogicalDevice() {
		var indices = FindQueueFamilies(_physicalDevice);

		var uniqueQueueFamilies = new[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };
		uniqueQueueFamilies = uniqueQueueFamilies.Distinct().ToArray();

		using var mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
		var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());

		var queuePriority = 1.0f;
		for (var i = 0; i < uniqueQueueFamilies.Length; i++) {
			queueCreateInfos[i] = new DeviceQueueCreateInfo
			{
				SType = StructureType.DeviceQueueCreateInfo,
				QueueFamilyIndex = uniqueQueueFamilies[i],
				QueueCount = 1,
				PQueuePriorities = &queuePriority
			};
		}

		var features = new PhysicalDeviceFeatures();
		
		var deviceCreateInfo = new DeviceCreateInfo {
			SType = StructureType.DeviceCreateInfo,
			PQueueCreateInfos = queueCreateInfos,
			QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
			PEnabledFeatures = &features,
			PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(DeviceExtensions),
			EnabledExtensionCount = (uint)DeviceExtensions.Length,
			EnabledLayerCount = (uint)ValidationLayers.Length,
			PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers)
		};

		if (vk!.CreateDevice(_physicalDevice, deviceCreateInfo, null, out _device) != Result.Success) {
			throw new Exception("Failed to create device");
		}

		vk!.GetDeviceQueue(_device, indices.GraphicsFamily.Value, 0, out _graphicsQueue);
		vk!.GetDeviceQueue(_device, indices.PresentFamily.Value, 0, out _presentQueue);

		SilkMarshal.Free((nint)deviceCreateInfo.PpEnabledLayerNames);
	}

	private void RecreateSwapChain() {
		Vector2D<int> framebufferSize;
		do {
			framebufferSize = window!.FramebufferSize;
			window!.DoEvents();
		} while (framebufferSize.X == 0 || framebufferSize.Y == 0);
		
		vk!.DeviceWaitIdle(_device);
		
		CleanupSwapchain();
		
		CreateSwapChain();
		CreateImageViews();
		CreateFramebuffers();
	}

	private void CleanupSwapchain() {
		foreach (var swapchainFramebuffer in swapchainFramebuffers) {
			vk!.DestroyFramebuffer(_device, swapchainFramebuffer, null);
		}
		foreach (var imageView in swapchainImageViews) {
			vk!.DestroyImageView(_device, imageView, null);
		}
		_khrSwapchain!.DestroySwapchain(_device, swapchain, null);
	}

	private void CreateSwapChain() {
		var support = QuerySwapChainSupport(_physicalDevice);
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

		var indices = FindQueueFamilies(_physicalDevice);
		var queueFamilyIndices = stackalloc [] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };
		if (indices.GraphicsFamily != indices.PresentFamily) {
			swapchainCreateInfo.ImageSharingMode = SharingMode.Concurrent;
			swapchainCreateInfo.QueueFamilyIndexCount = 2;
			swapchainCreateInfo.PQueueFamilyIndices = queueFamilyIndices;
		}
		else {
			swapchainCreateInfo.ImageSharingMode = SharingMode.Exclusive;
			swapchainCreateInfo.QueueFamilyIndexCount = 0;
			swapchainCreateInfo.PQueueFamilyIndices = null;
		}
		
		if (!vk!.TryGetDeviceExtension(instance, _device, out _khrSwapchain))
		{
			throw new NotSupportedException("VK_KHR_swapchain extension not found.");
		}

		if (_khrSwapchain!.CreateSwapchain(_device, swapchainCreateInfo, null, out swapchain) != Result.Success) {
			throw new Exception("Failed to create swapchain");
		}

		swapchainImages = Helpers.GetArray((ref uint length, Image* data) =>
			_khrSwapchain.GetSwapchainImages(_device, swapchain, ref length, data));

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

		var framebufferSize = window!.FramebufferSize;

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
			var imageViewCreateInfo = new ImageViewCreateInfo {
				SType = StructureType.ImageViewCreateInfo,
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

			if (vk!.CreateImageView(_device, imageViewCreateInfo, null, out swapchainImageViews[i]) != Result.Success) {
				throw new Exception("Failed to create an image view");
			}
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
		
		var createInfo = new RenderPassCreateInfo {
			SType	= StructureType.RenderPassCreateInfo,
			AttachmentCount = 1,
			PAttachments = &colorAttachment,
			SubpassCount = 1,
			PSubpasses = &subpass,
			DependencyCount = 1,
			PDependencies = &subpassDependency
		};

		if (vk!.CreateRenderPass(_device, createInfo, null, out renderPass) != Result.Success) {
			throw new Exception("Could not create the render pass");
		}
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

			var pipelineLayoutInfo = new PipelineLayoutCreateInfo {
				SType = StructureType.PipelineLayoutCreateInfo
			};

			if (vk!.CreatePipelineLayout(_device, pipelineLayoutInfo, null, out pipelineLayout) != Result.Success) {
				throw new Exception("Failed to create pipeline layout");
			}

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

				if (vk!.CreateGraphicsPipelines(_device, default, 1, &pipelineInfo, null, out graphicsPipeline) !=
				    Result.Success) {
					throw new Exception("Failed to create graphics pipelines");
				}
			}
		}

		SilkMarshal.Free((nint)vertShaderStageInfo.PName);
		SilkMarshal.Free((nint)fragShaderStageInfo.PName);
		vk!.DestroyShaderModule(_device, vertShaderModule, null);
		vk!.DestroyShaderModule(_device, fragShaderModule, null);
	}

	private ShaderModule CreateShaderModule(byte[] code) {
		fixed (byte* codePointer = code) {
			var createInfo = new ShaderModuleCreateInfo {
				SType = StructureType.ShaderModuleCreateInfo,
				CodeSize = (uint)code.Length,
				PCode = (uint*)codePointer
			};
			if (vk!.CreateShaderModule(_device, createInfo, null, out var shaderModule) != Result.Success) {
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

			if (vk!.CreateFramebuffer(_device, framebufferInfo, null, out swapchainFramebuffers[i]) != Result.Success) {
				throw new Exception("Failed to create framebuffer");
			}
		}
	}

	private void CreateCommandPool() {
		var queueFamilyIndices = FindQueueFamilies(_physicalDevice);

		var commandPoolCreateInfo = new CommandPoolCreateInfo {
			SType = StructureType.CommandPoolCreateInfo,
			QueueFamilyIndex = queueFamilyIndices.GraphicsFamily!.Value,
			Flags = CommandPoolCreateFlags.ResetCommandBufferBit
		};

		if (vk!.CreateCommandPool(_device, commandPoolCreateInfo, null, out commandPool) != Result.Success) {
			throw new Exception("Failed to create command pool");
		}
	}

	private void CreateVertexBuffer() {
		var bufferCreateInfo = new BufferCreateInfo {
			SType = StructureType.BufferCreateInfo,
			Size = (uint)(Unsafe.SizeOf<Vertex>() * vertices.Length),
			Usage = BufferUsageFlags.VertexBufferBit,
			SharingMode = SharingMode.Exclusive
		};

		if (vk!.CreateBuffer(_device, bufferCreateInfo, null, out vertexBuffer) != Result.Success) {
			throw new Exception("Failed to create Vertex Buffer");
		}

		vk!.GetBufferMemoryRequirements(_device, vertexBuffer, out var memoryRequirements);

		var allocInfo = new MemoryAllocateInfo {
			SType = StructureType.MemoryAllocateInfo,
			AllocationSize = memoryRequirements.Size,
			MemoryTypeIndex = FindMemoryType(memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
		};

		if (vk!.AllocateMemory(_device, allocInfo, null, out vertexBufferMemory) != Result.Success) {
			throw new Exception("Failed to create Vertex Buffer memory");
		}

		vk.BindBufferMemory(_device, vertexBuffer, vertexBufferMemory, 0);

		void* data;
		vk!.MapMemory(_device, vertexBufferMemory, 0, bufferCreateInfo.Size, 0, &data);
		vertices.AsSpan().CopyTo(new Span<Vertex>(data, vertices.Length));
		vk!.UnmapMemory(_device, vertexBufferMemory);
	}

	private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties) {
		vk!.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memoryProperties);

		for (var i = 0; i < memoryProperties.MemoryTypeCount; i++) {
			var memType = memoryProperties.MemoryTypes[i];
			if ((typeFilter & (1 << i)) != 0 && (memType.PropertyFlags & properties) == properties) {
				return (uint)i;
			}
		}

		throw new Exception("Failed to find a suitable memory location");
	}

	private void CreateCommandBuffers() {
		var commandBuffers = stackalloc CommandBuffer[renderFrames.Length];
		var allocInfo = new CommandBufferAllocateInfo {
			SType = StructureType.CommandBufferAllocateInfo,
			CommandPool = commandPool,
			CommandBufferCount = (uint)renderFrames.Length,
			Level = CommandBufferLevel.Primary
		};
		if (vk!.AllocateCommandBuffers(_device, allocInfo, commandBuffers) != Result.Success) {
			throw new Exception("Failed to create command buffer");
		}

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
			if (vk!.CreateSemaphore(_device, semaphoreInfo, null, out frame.ImageAvailableSemaphore) != Result.Success
			    || vk!.CreateSemaphore(_device, semaphoreInfo, null, out frame.RenderFinishedSemaphore) != Result.Success
			    || vk!.CreateFence(_device, fenceInfo, null, out frame.InFlightFence) != Result.Success) {
				throw new Exception("Failed to create sync objects");
			}
		}
	}

	private void RecordCommandBuffer(CommandBuffer buffer, int index) {
		var beginInfo = new CommandBufferBeginInfo {
			SType = StructureType.CommandBufferBeginInfo
		};
		if (vk!.BeginCommandBuffer(buffer, beginInfo) != Result.Success) {
			throw new Exception("Failed to begin the command buffer");
		}

		var clearValue = new ClearValue {
			Color = new ClearColorValue(0, 0, 0, 1)
		};
		var renderPassBegin = new RenderPassBeginInfo {
			SType = StructureType.RenderPassBeginInfo,
			RenderPass = renderPass,
			Framebuffer = swapchainFramebuffers[index],
			RenderArea = new Rect2D(new Offset2D(0, 0), swapchainExtent),
			ClearValueCount = 1,
			PClearValues = &clearValue
		};
		vk!.CmdBeginRenderPass(buffer, renderPassBegin, SubpassContents.Inline);
		vk!.CmdBindPipeline(buffer, PipelineBindPoint.Graphics, graphicsPipeline);

		vk!.CmdBindVertexBuffers(buffer, 0, 1, vertexBuffer, 0);

		var viewport = new Viewport {
			Height = swapchainExtent.Height,
			Width = swapchainExtent.Width,
			X = 0,
			Y = 0,
			MaxDepth = 1,
			MinDepth = 0
		};
		vk!.CmdSetViewport(buffer, 0, 1, &viewport);

		var scissor = new Rect2D {
			Offset = new Offset2D(0, 0),
			Extent = swapchainExtent
		};
		vk!.CmdSetScissor(buffer, 0, 1, &scissor);

		vk!.CmdDraw(buffer, (uint)vertices.Length, 1, 0, 0);

		vk!.CmdEndRenderPass(buffer);

		if (vk!.EndCommandBuffer(buffer) != Result.Success) {
			throw new Exception("Failed to end the command buffer");
		}
	}

	private void MainLoop() {
		window!.Render += DrawFrame;
		window!.Resize += _ => framebufferResized = true;
		window!.Run();
		vk!.DeviceWaitIdle(_device);
	}

	private void DrawFrame(double dt) {
		var frame = renderFrames[currentFrame];
		vk!.WaitForFences(_device, 1, frame.InFlightFence, true, int.MaxValue);

		uint imageIndex = 0;
		var acquireResult = _khrSwapchain!.AcquireNextImage(_device, swapchain, int.MaxValue, frame.ImageAvailableSemaphore, default, ref imageIndex);
		if (acquireResult == Result.ErrorOutOfDateKhr) {
			RecreateSwapChain();
			return;
		}
		if (acquireResult != Result.Success && acquireResult != Result.SuboptimalKhr) {
			throw new Exception("failed to acquire next image");
		}
		
		vk!.ResetFences(_device, 1, frame.InFlightFence);
		
		vk!.ResetCommandBuffer(frame.CommandBuffer, 0);
		RecordCommandBuffer(frame.CommandBuffer, (int)imageIndex);

		var buffer = frame.CommandBuffer;
		var waitSemaphores = stackalloc[] { frame.ImageAvailableSemaphore };
		var pipelineStageFlags = stackalloc [] { PipelineStageFlags.ColorAttachmentOutputBit };
		var signalSemaphores = stackalloc[] { frame.RenderFinishedSemaphore };
		var submitInfo = new SubmitInfo {
			SType = StructureType.SubmitInfo,
			WaitSemaphoreCount = 1,
			PWaitSemaphores = waitSemaphores,
			PWaitDstStageMask = pipelineStageFlags,
			CommandBufferCount = 1,
			PCommandBuffers = &buffer,
			SignalSemaphoreCount = 1,
			PSignalSemaphores = signalSemaphores
		};

		if (vk!.QueueSubmit(_graphicsQueue, 1, submitInfo, frame.InFlightFence) != Result.Success) {
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
		var presentResult = _khrSwapchain.QueuePresent(_presentQueue, presentInfo);
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
		vk!.DestroyBuffer(_device, vertexBuffer, null);
		foreach (var frame in renderFrames) {
			vk!.DestroySemaphore(_device, frame.ImageAvailableSemaphore, null);
			vk!.DestroySemaphore(_device, frame.RenderFinishedSemaphore, null);
			vk!.DestroyFence(_device, frame.InFlightFence, null);
		}
		vk!.DestroyCommandPool(_device, commandPool, null);
		
		vk!.DestroyPipeline(_device, graphicsPipeline, null);
		vk!.DestroyPipelineLayout(_device, pipelineLayout, null);
		vk!.DestroyRenderPass(_device, renderPass, null);
		
		CleanupSwapchain();

		vk!.DestroyBuffer(_device, vertexBuffer, null);
		vk!.FreeMemory(_device, vertexBufferMemory, null);
		
		vk!.DestroyDevice(_device, null);
		if (EnableValidationLayers) {
			debugUtils!.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
		}

		_khrSurface!.DestroySurface(instance, _surface, null);
		vk!.DestroyInstance(instance, null);
		vk!.Dispose();
		window?.Dispose();
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