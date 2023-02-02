using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Assimp;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using SilkNetConvenience;
using SilkNetConvenience.Assimp.Wrappers;
using SilkNetConvenience.Barriers;
using SilkNetConvenience.Buffers;
using SilkNetConvenience.CommandBuffers;
using SilkNetConvenience.Descriptors;
using SilkNetConvenience.Devices;
using SilkNetConvenience.Exceptions.ResultExceptions;
using SilkNetConvenience.EXT;
using SilkNetConvenience.Images;
using SilkNetConvenience.Instances;
using SilkNetConvenience.KHR;
using SilkNetConvenience.Memory;
using SilkNetConvenience.Pipelines;
using SilkNetConvenience.Queues;
using SilkNetConvenience.RenderPasses;
using SixLabors.ImageSharp.PixelFormats;
using VideoWall.Exceptions;
using File = System.IO.File;

namespace VideoWall; 

public unsafe class HelloTriangleApplication
{
	private const int MaxFramesInFlight = 2;
	private const int WIDTH = 800;
	private const int HEIGHT = 600;

	private const string MODEL_PATH = "models/viking_room.obj";
	private const string TEXTURE_PATH = "models/viking_room.png";

	private readonly string[] ValidationLayers = new[] {
		"VK_LAYER_KHRONOS_validation"
	};
	public bool EnableValidationLayers = true;

	private readonly string[] DeviceExtensions = {
		KhrSwapchain.ExtensionName
	};

	private readonly Illustrate.Window window;
	private readonly VulkanContext vk;
	
	private VulkanSwapchain? swapchain;
	private VulkanSwapchainImage[] swapchainImages = Array.Empty<VulkanSwapchainImage>();
	private Format swapchainFormat;
	private Extent2D swapchainExtent;

	private VulkanImageView[] swapchainImageViews = Array.Empty<VulkanImageView>();
	private VulkanFramebuffer[] swapchainFramebuffers = Array.Empty<VulkanFramebuffer>();

	private readonly RenderFrame[] renderFrames = new RenderFrame[MaxFramesInFlight];
	private int currentFrame;

	private bool framebufferResized;

	private VulkanBuffer? vertexBuffer;
	private VulkanBuffer? indexBuffer;

	private VulkanImage? textureImage;
	private VulkanImageView? textureImageView;
	private VulkanSampler? textureSampler;

	private VulkanImage? depthImage;
	private VulkanDeviceMemory? depthImageMemory;
	private VulkanImageView? depthImageView;

	private readonly List<Vertex> vertices = new();
	private readonly List<uint> indices = new();

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
		var (instance, physicalDevice, device, graphicsQueue, presentQueue, 
			surface, commandPool, renderPass, pipelineLayout, graphicsPipeline) = InitVulkan();
		MainLoop(instance, physicalDevice, device, graphicsQueue, presentQueue, 
				 surface, commandPool, renderPass, pipelineLayout, graphicsPipeline);
		CleanUp(instance);
	}

	private (VulkanInstance, VulkanPhysicalDevice, VulkanDevice, 
		VulkanQueue graphicsQueue, VulkanQueue presentQueue, VulkanSurface surface,
		VulkanCommandPool commandPool, VulkanRenderPass renderPass, VulkanPipelineLayout,
		VulkanPipeline graphicsPipeline) InitVulkan() {
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
		var descriptorSetLayout = CreateDescriptorSetLayout(device);
		var pipelineLayout = device.CreatePipelineLayout(descriptorSetLayout);
		var graphicsPipeline = CreateGraphicsPipeline(device, renderPass, pipelineLayout);
		var commandPool = CreateCommandPool(instance, physicalDevice, device, surface);
		CreateDepthResources(physicalDevice, device, graphicsQueue, commandPool);
		CreateFramebuffers(device, renderPass);
		CreateTextureImage(device, graphicsQueue, commandPool);
		CreateTextureImageView(device);
		CreateTextureSampler(physicalDevice, device);
		LoadModel();
		CreateVertexBuffer(device, graphicsQueue, commandPool);
		CreateIndexBuffer(device, graphicsQueue, commandPool);
		CreateUniformBuffers(device);
		var descriptorPool = CreateDescriptorPool(device);
		CreateDescriptorSets(device, descriptorPool, descriptorSetLayout);
		CreateCommandBuffers(commandPool);
		CreateSyncObjects(device);

		return (instance, physicalDevice, device, graphicsQueue, presentQueue, surface, commandPool, renderPass, 
				   pipelineLayout, graphicsPipeline);
	}

	private void LoadModel() {
		using var assimp = new AssimpContext();
		using var scene = assimp.ImportFile(MODEL_PATH, (uint)PostProcessPreset.TargetRealTimeMaximumQuality);
		var vertexMap = new Dictionary<Vertex, uint>();
		VisitSceneNode(vertexMap, scene.RootNode);
	}

	private void VisitSceneNode(IDictionary<Vertex, uint> vertexMap, AssimpNode node) {
		foreach (var mesh in node.Meshes) {
			foreach (var face in mesh.Faces) {
				foreach (var index in face.Indices) {
					var position = mesh.Vertices[index];
					var texture = mesh.TextureCoords[0][(int)index];

					var vertex = new Vertex
					{
						Position = new Vector3D<float>(position.X, position.Y, position.Z),
						Color = new Vector3D<float>(1, 1, 1),
						//Flip Y for OBJ in Vulkan
						TexCoord = new Vector2D<float>(texture.X, 1.0f - texture.Y)
					};

					if (vertexMap.TryGetValue(vertex, out var meshIndex)) {
						indices.Add(meshIndex);
					}
					else {
						indices.Add((uint)vertices.Count);
						vertexMap[vertex] = (uint)vertices.Count;
						vertices.Add(vertex);
					}                        
				}
			}
		}

		foreach (var child in node.Children) {
			VisitSceneNode(vertexMap, child);
		}
	}

	private void CreateDepthResources(VulkanPhysicalDevice physicalDevice, VulkanDevice device, 
									  VulkanQueue graphicsQueue, VulkanCommandPool commandPool) {
		var depthFormat = FindDepthFormat(physicalDevice);
		(depthImage, depthImageMemory) = CreateImage(device, swapchainExtent.Width, swapchainExtent.Height, depthFormat, ImageTiling.Optimal,
			ImageUsageFlags.DepthStencilAttachmentBit, MemoryPropertyFlags.DeviceLocalBit);
		depthImageView = CreateImageView(device, depthImage, depthFormat, ImageAspectFlags.DepthBit);

		TransitionImageLayout(graphicsQueue, depthImage, depthFormat, ImageLayout.Undefined,
			ImageLayout.DepthStencilAttachmentOptimal, commandPool);
	}

	private bool HasStencilComponent(Format format) {
		return format is Format.D32SfloatS8Uint or Format.D24UnormS8Uint;
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

	private void CreateTextureSampler(VulkanPhysicalDevice physicalDevice, VulkanDevice device) {
		var properties = physicalDevice.GetProperties();
		var createInfo = new SamplerCreateInformation {
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
		};
		textureSampler = device.CreateSampler(createInfo);
	}

	private void CreateTextureImageView(VulkanDevice device) {
		textureImageView = CreateImageView(device, textureImage!, Format.R8G8B8A8Srgb, ImageAspectFlags.ColorBit);
	}

	private VulkanImageView CreateImageView(VulkanDevice device, VulkanImage image, Format format, ImageAspectFlags aspectFlags) {
		var viewInfo = new ImageViewCreateInformation {
			Image = image.Image,
			ViewType = ImageViewType.Type2D,
			Format = format,
			SubresourceRange = new ImageSubresourceRange {
				AspectMask = aspectFlags,
				BaseMipLevel = 0,
				LevelCount = 1,
				BaseArrayLayer = 0,
				LayerCount = 1
			}
		};
		return device.CreateImageView(viewInfo);
	}

	private void CreateTextureImage(VulkanDevice device, VulkanQueue graphicsQueue, VulkanCommandPool commandPool) {
		var image = SixLabors.ImageSharp.Image.Load(TEXTURE_PATH);
		var imageSize = image.Width * image.Height * 4;

		var (stagingBuffer, stagingBufferMemory) = CreateBuffer(device, (uint)imageSize, BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit);
		
		using (stagingBuffer)
		using (stagingBufferMemory) {
			var data = stagingBufferMemory.MapMemory();
			image.CloneAs<Rgba32>().CopyPixelDataTo(data);
			stagingBufferMemory.UnmapMemory();

			(textureImage, _) = CreateImage(device, (uint)image.Width, (uint)image.Height, Format.R8G8B8A8Srgb,
				ImageTiling.Optimal, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
				MemoryPropertyFlags.DeviceLocalBit);

			TransitionImageLayout(graphicsQueue, textureImage, Format.R8G8B8A8Srgb, ImageLayout.Undefined,
				ImageLayout.TransferDstOptimal, commandPool);
			CopyBufferToImage(graphicsQueue, stagingBuffer, textureImage, (uint)image.Width, (uint)image.Height, commandPool);
			TransitionImageLayout(graphicsQueue, textureImage, Format.R8G8B8A8Srgb, ImageLayout.TransferDstOptimal,
				ImageLayout.ShaderReadOnlyOptimal, commandPool);
		}
	}

	private (VulkanImage image, VulkanDeviceMemory imageMemory) CreateImage(VulkanDevice device, 
		uint width, uint height, Format format,
		ImageTiling imageTiling, ImageUsageFlags imageUsageFlags, MemoryPropertyFlags memoryPropertyFlags) {
		
		var imageInfo = new ImageCreateInformation {
			ImageType = ImageType.Type2D,
			Extent = new Extent3D {
				Width = width,
				Height = height,
				Depth = 1
			},
			MipLevels = 1,
			ArrayLayers = 1,
			Format = format,
			Tiling = imageTiling,
			InitialLayout = ImageLayout.Undefined,
			Usage = imageUsageFlags,
			SharingMode = SharingMode.Exclusive,
			Samples = SampleCountFlags.Count1Bit,
			Flags = ImageCreateFlags.None
		};

		var image = device.CreateImage(imageInfo);
		var imageMemory = device.AllocateMemoryFor(image, memoryPropertyFlags);
		image.BindMemory(imageMemory);
		return (image, imageMemory);
	}

	private void TransitionImageLayout(VulkanQueue graphicsQueue, VulkanImage image, Format format, 
									   ImageLayout oldLayout, ImageLayout newLayout, VulkanCommandPool commandPool) {
		graphicsQueue.SubmitSingleUseCommandBufferAndWaitIdle(commandPool, command => {
			ImageAspectFlags aspectFlags;
			if (newLayout == ImageLayout.DepthStencilAttachmentOptimal) {
				aspectFlags = ImageAspectFlags.DepthBit;
				if (HasStencilComponent(format)) {
					aspectFlags |= ImageAspectFlags.StencilBit;
				}
			}
			else {
				aspectFlags = ImageAspectFlags.ColorBit;
			}
			var barrier = new ImageMemoryBarrierInformation {
				OldLayout = oldLayout,
				NewLayout = newLayout,
				SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
				DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
				Image = image.Image,
				SubresourceRange = new ImageSubresourceRange {
					AspectMask = aspectFlags,
					BaseMipLevel = 0,
					LevelCount = 1,
					BaseArrayLayer = 0,
					LayerCount = 1
				}
			};

			PipelineStageFlags sourceFlags;
			PipelineStageFlags destinationFlags;

			if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal) {
				barrier.SrcAccessMask = AccessFlags.None;
				barrier.DstAccessMask = AccessFlags.TransferWriteBit;
				sourceFlags = PipelineStageFlags.TopOfPipeBit;
				destinationFlags = PipelineStageFlags.TransferBit;
			} else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal) {
				barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
				barrier.DstAccessMask = AccessFlags.ShaderReadBit;
				sourceFlags = PipelineStageFlags.TransferBit;
				destinationFlags = PipelineStageFlags.FragmentShaderBit;
			} else if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.DepthStencilAttachmentOptimal) {
				barrier.SrcAccessMask = AccessFlags.None;
				barrier.DstAccessMask = AccessFlags.DepthStencilAttachmentReadBit |
				                        AccessFlags.DepthStencilAttachmentWriteBit;
				sourceFlags = PipelineStageFlags.TopOfPipeBit;
				destinationFlags = PipelineStageFlags.EarlyFragmentTestsBit;
			}
			else {
				throw new NotSupportedException();
			}
			
			command.PipelineBarrier(sourceFlags, destinationFlags,
				DependencyFlags.None, barrier);
		});
	}

	private void CopyBufferToImage(VulkanQueue graphicsQueue, VulkanBuffer buffer, VulkanImage image, 
								   uint width, uint height, VulkanCommandPool commandPool) {
		graphicsQueue.SubmitSingleUseCommandBufferAndWaitIdle(commandPool, cmd => {
			var region = new BufferImageCopy {
				BufferOffset = 0,
				BufferRowLength = 0,
				BufferImageHeight = 0,
				ImageSubresource = new ImageSubresourceLayers {
					AspectMask = ImageAspectFlags.ColorBit,
					MipLevel = 0,
					BaseArrayLayer = 0,
					LayerCount = 1
				},
				ImageOffset = new Offset3D(0, 0, 0),
				ImageExtent = new Extent3D(width, height, 1)
			};
			cmd.CopyBufferToImage(buffer, image, ImageLayout.TransferDstOptimal, region);
		});
	}

	private void CreateDescriptorSets(VulkanDevice device, VulkanDescriptorPool descriptorPool, VulkanDescriptorSetLayout descriptorSetLayout) {
		var descriptorSets = descriptorPool.AllocateDescriptorSets(MaxFramesInFlight, descriptorSetLayout);
		for (var i = 0; i < MaxFramesInFlight; i++) {
			var frame = renderFrames[i];
			frame.DescriptorSet = descriptorSets[i];

			var bufferInfo = new DescriptorBufferInfo {
				Buffer = frame.UniformBuffer!.Buffer,
				Offset = 0,
				Range = (uint)sizeof(UniformBufferObject)
			};
			var imageInfo = new DescriptorImageInfo {
				Sampler = textureSampler!.Sampler,
				ImageView = textureImageView!.ImageView,
				ImageLayout = ImageLayout.ShaderReadOnlyOptimal
			};
			var writeBufferInfo = new WriteDescriptorSetInformation {
				DstSet = frame.DescriptorSet.DescriptorSet,
				DstBinding = 0,
				DstArrayElement = 0,
				DescriptorType = DescriptorType.UniformBuffer,
				DescriptorCount = 1,
				BufferInfo = new[]{bufferInfo}
			};
			var writeImageInfo = new WriteDescriptorSetInformation {
				DstSet = frame.DescriptorSet.DescriptorSet,
				DstBinding = 1,
				DescriptorType = DescriptorType.CombinedImageSampler,
				DescriptorCount = 1,
				DstArrayElement = 0,
				ImageInfo = new[]{imageInfo}
			};
			device.UpdateDescriptorSets(writeBufferInfo, writeImageInfo);
		}
	}

	private VulkanDescriptorPool CreateDescriptorPool(VulkanDevice device) {
		var createInfo = new DescriptorPoolCreateInformation {
			PoolSizes = new [] {
				new DescriptorPoolSize {
					Type = DescriptorType.UniformBuffer,
					DescriptorCount = MaxFramesInFlight
				},
				new DescriptorPoolSize {
					Type = DescriptorType.CombinedImageSampler,
					DescriptorCount = MaxFramesInFlight
				}
			},
			MaxSets = MaxFramesInFlight
		};
		return device.CreateDescriptorPool(createInfo);
	}

	private void CreateUniformBuffers(VulkanDevice device) {
		var bufferSize = (uint)sizeof(UniformBufferObject);

		for (var i = 0; i < MaxFramesInFlight; i++) {
			var frame = renderFrames[i];

			(frame.UniformBuffer, frame.UniformBufferMemory) = CreateBuffer(device, bufferSize,
				BufferUsageFlags.UniformBufferBit,
				MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
		}
	}

	private VulkanDescriptorSetLayout CreateDescriptorSetLayout(VulkanDevice device) {
		DescriptorSetLayoutBindingInformation uboLayoutBinding = new() {
			Binding = 0,
			DescriptorType = DescriptorType.UniformBuffer,
			DescriptorCount = 1,
			StageFlags = ShaderStageFlags.VertexBit
		};

		var samplerLayoutBinding = new DescriptorSetLayoutBindingInformation {
			Binding = 1,
			DescriptorCount = 1,
			DescriptorType = DescriptorType.CombinedImageSampler,
			StageFlags = ShaderStageFlags.FragmentBit
		};
		
		var createInfo = new DescriptorSetLayoutCreateInformation {
			Bindings = new[] { uboLayoutBinding, samplerLayoutBinding }
		};
		return device.CreateDescriptorSetLayout(createInfo);
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

	private void SetupDebugMessenger(VulkanInstance instance) {
		if (!EnableValidationLayers) return;

		var createInfo = new DebugUtilsMessengerCreateInformation();
		PopulateDebugMessengerCreateInfo(ref createInfo);

		instance.DebugUtils.CreateDebugUtilsMessenger(createInfo);
	}

	private static void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInformation createInfo) {
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
		var propertyNames = properties.Select(p => SilkMarshal.PtrToString((nint)p.ExtensionName)).ToList();
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
		depthImageView!.Dispose();
		depthImage!.Dispose();
		depthImageMemory!.Dispose();

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
			swapchainImageViews[i] = CreateImageView(device, swapchainImages[i], swapchainFormat, ImageAspectFlags.ColorBit);
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
				CullMode = CullModeFlags.FrontBit,
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
				Attachments = new[]{imageView, depthImageView!.ImageView},
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

	private void CreateVertexBuffer(VulkanDevice device, VulkanQueue graphicsQueue, VulkanCommandPool commandPool) {
		var bufferSize = (uint)(Unsafe.SizeOf<Vertex>() * vertices.Count);
		var (stagingBuffer, stagingBufferMemory) = CreateBuffer(device, bufferSize, BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
		
		using (stagingBuffer)
		using (stagingBufferMemory) {
			var data = stagingBufferMemory.MapMemory<Vertex>();
			vertices.ToArray().AsSpan().CopyTo(data);
			stagingBufferMemory.UnmapMemory();
		
			(vertexBuffer, _) = CreateBuffer(device, bufferSize, 
				BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, 
				MemoryPropertyFlags.DeviceLocalBit);

			CopyBuffer(graphicsQueue, stagingBuffer, vertexBuffer, bufferSize, commandPool);
		}
	}

	private void CreateIndexBuffer(VulkanDevice device, VulkanQueue graphicsQueue, VulkanCommandPool commandPool) {
		var bufferSize =  sizeof(int) * (uint)indices.Count;

		var (stagingBuffer, stagingMemory) = CreateBuffer(device, bufferSize, BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit);

		using (stagingBuffer)
		using (stagingMemory) {
			var data = stagingMemory.MapMemory<uint>();
			indices.ToArray().AsSpan().CopyTo(data);
			stagingMemory.UnmapMemory();

			(indexBuffer, _) = CreateBuffer(device, bufferSize,
				BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
				MemoryPropertyFlags.DeviceLocalBit);

			CopyBuffer(graphicsQueue, stagingBuffer, indexBuffer, bufferSize, commandPool);
		}
	}

	private void CopyBuffer(VulkanQueue graphicsQueue, VulkanBuffer source, VulkanBuffer destination, uint size,
							VulkanCommandPool commandPool) {
		graphicsQueue.SubmitSingleUseCommandBufferAndWaitIdle(commandPool, buffer => {
			buffer.CopyBuffer(source, destination, size);
		});
	}

	private (VulkanBuffer buffer, VulkanDeviceMemory memory) CreateBuffer(VulkanDevice device, uint size,
																		  BufferUsageFlags usage,
																		  MemoryPropertyFlags properties) {
		var bufferCreateInfo = new BufferCreateInformation {
			Size = size,
			Usage = usage,
			SharingMode = SharingMode.Exclusive
		};
		var buffer = device.CreateBuffer(bufferCreateInfo);
		var bufferMemory = device.AllocateMemoryFor(buffer, properties);
		buffer.BindMemory(bufferMemory);
		return (buffer, bufferMemory);
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

	private void RecordCommandBuffer(VulkanCommandBuffer buffer, int index, VulkanRenderPass renderPass, 
									 VulkanPipelineLayout pipelineLayout, VulkanPipeline graphicsPipeline) {
		buffer.Begin();
		
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
		
		buffer.BeginRenderPass(renderPassBegin, SubpassContents.Inline);
		buffer.BindPipeline(graphicsPipeline);
		
		buffer.BindVertexBuffer(0, vertexBuffer!.Buffer);
		buffer.BindIndexBuffer(indexBuffer!, 0, IndexType.Uint32);

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

		buffer.BindDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, 0, renderFrames[currentFrame].DescriptorSet!);
		buffer.DrawIndexed((uint)indices.Count);

		buffer.EndRenderPass();

		buffer.End();
	}

	private void MainLoop(VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanDevice device, 
						  VulkanQueue graphicsQueue, VulkanQueue presentQueue, VulkanSurface surface, 
						  VulkanCommandPool commandPool, VulkanRenderPass renderPass, VulkanPipelineLayout pipelineLayout,
						  VulkanPipeline graphicsPipeline) {
		window.Render += dt => DrawFrame(dt, instance, physicalDevice, device, graphicsQueue,
										 presentQueue, surface, commandPool, renderPass, pipelineLayout, graphicsPipeline);
		window.Resize += _ => framebufferResized = true;
		window.Run();
		device.WaitIdle();
	}

	private double currentTime;

	private void DrawFrame(double dt, VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanDevice device, 
						   VulkanQueue graphicsQueue, VulkanQueue presentQueue, VulkanSurface surface,
						   VulkanCommandPool commandPool, VulkanRenderPass renderPass, VulkanPipelineLayout pipelineLayout,
						   VulkanPipeline graphicsPipeline) {
		currentTime += dt;
		
		var frame = renderFrames[currentFrame];
		frame.InFlightFence!.Wait();

		uint imageIndex;
		try {
			imageIndex = swapchain!.AcquireNextImage(frame.ImageAvailableSemaphore!);
		}
		catch (ErrorOutOfDateKhrException)
		{
			RecreateSwapchain(instance, physicalDevice, device, graphicsQueue, surface, commandPool, renderPass);
			return;
		}
		catch (SuboptimalKhrException)
		{
			RecreateSwapchain(instance, physicalDevice, device, graphicsQueue, surface, commandPool, renderPass);
			return;
		}

		frame.InFlightFence.Reset();

		frame.CommandBuffer!.Reset();
		RecordCommandBuffer(frame.CommandBuffer, (int)imageIndex, renderPass, pipelineLayout, graphicsPipeline);

		UpdateUniformBuffer(currentFrame);

		var buffer = frame.CommandBuffer;
		var signalSemaphores = new[] { frame.RenderFinishedSemaphore!.Semaphore };

		graphicsQueue.Submit(new SubmitInformation {
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
			device.KhrSwapchain.QueuePresent(presentQueue, presentInfo);
		}
		catch (ErrorOutOfDateKhrException) {
			framebufferResized = false;
			RecreateSwapchain(instance, physicalDevice, device, graphicsQueue, surface, commandPool, renderPass);
			return;
		}
		catch (SuboptimalKhrException) {
			framebufferResized = false;
			RecreateSwapchain(instance, physicalDevice, device, graphicsQueue, surface, commandPool, renderPass);
			return;
		}
		if (framebufferResized) {
			framebufferResized = false;
			RecreateSwapchain(instance, physicalDevice, device, graphicsQueue, surface, commandPool, renderPass);
			return;
		}
		currentFrame = (currentFrame + 1) % MaxFramesInFlight;
	}

	private const float Circle = 2 * MathF.PI; 

	private void UpdateUniformBuffer(int frame) {
		var time = (float)currentTime;

		UniformBufferObject ubo = new()
		{
			model = Matrix4X4.CreateFromAxisAngle(new Vector3D<float>(0,0,1), time * Circle / 4),
			view = Matrix4X4.CreateLookAt(
				new Vector3D<float>(2, 2, 2), 
				new Vector3D<float>(0, 0, 0), 
				new Vector3D<float>(0, 0, 1)),
			proj = Matrix4X4.CreatePerspectiveFieldOfView(Circle / 8, 
				swapchainExtent.Width / (float)swapchainExtent.Height, 
				0.1f, 10.0f),
		};
		ubo.proj.M22 *= -1;

		var memory = renderFrames[frame].UniformBufferMemory!;
		var data = memory.MapMemory<UniformBufferObject>(0, (uint)sizeof(UniformBufferObject));
		data[0] = ubo;
		memory.UnmapMemory();
	}

	private void CleanUp(VulkanInstance instance) {
		foreach (var frame in renderFrames) {
			frame.ImageAvailableSemaphore!.Dispose();
			frame.RenderFinishedSemaphore!.Dispose();
			frame.InFlightFence!.Dispose();
		}

		CleanupSwapchain();

		instance.Dispose();
		vk.Dispose();
		window.Dispose();
	}
	
	private static uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, 
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
}