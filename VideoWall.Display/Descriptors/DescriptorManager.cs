using Silk.NET.Vulkan;
using SilkNetConvenience.Buffers;
using SilkNetConvenience.Descriptors;
using SilkNetConvenience.Devices;
using SilkNetConvenience.Images;

namespace VideoWall.Display.Descriptors; 

public class DescriptorManager {
	private readonly VulkanDevice _device;
	public VulkanDescriptorSetLayout DescriptorSetLayout { get; }
	private readonly DescriptorCollection<DescriptorKey> _descriptors;

	public DescriptorManager(VulkanDevice device) {
		_device = device;
		const uint maxPoolSize = 100;
		var descriptorPool = device.CreateDescriptorPool(new DescriptorPoolCreateInformation {
			PoolSizes = new [] {
				new DescriptorPoolSize {
					Type = DescriptorType.UniformBuffer,
					DescriptorCount = maxPoolSize
				},
				new DescriptorPoolSize {
					Type = DescriptorType.CombinedImageSampler,
					DescriptorCount = maxPoolSize
				}
			},
			MaxSets = maxPoolSize
		});

		DescriptorSetLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutCreateInformation {
			Bindings = new[] {
				new() {
					Binding = 0,
					DescriptorType = DescriptorType.UniformBuffer,
					DescriptorCount = 1,
					StageFlags = ShaderStageFlags.VertexBit
				},
				new DescriptorSetLayoutBindingInformation {
					Binding = 1,
					DescriptorCount = 1,
					DescriptorType = DescriptorType.CombinedImageSampler,
					StageFlags = ShaderStageFlags.FragmentBit
				}
			}
		});

		_descriptors = new DescriptorCollection<DescriptorKey>(descriptorPool, DescriptorSetLayout);
	}

	public unsafe VulkanDescriptorSet UpdateDescriptorSet(uint frameIndex, VulkanBuffer buffer, VulkanImageView imageView, VulkanSampler sampler) {
		var key = new DescriptorKey {
			FrameIndex = frameIndex,
			ImageView = imageView,
			UBO = buffer
		};
		var set = _descriptors.GetSet(key);
		var writeBufferInfo = new WriteDescriptorSetInformation {
			DstSet = set,
			DstBinding = 0,
			DstArrayElement = 0,
			DescriptorType = DescriptorType.UniformBuffer,
			DescriptorCount = 1,
			BufferInfo = new[] {
				new DescriptorBufferInfo {
					Buffer = buffer,
					Offset = 0,
					Range = (uint)sizeof(UniformBufferObject)
				}
			}
		};
		var writeImageInfo = new WriteDescriptorSetInformation {
			DstSet = set,
			DstBinding = 1,
			DescriptorType = DescriptorType.CombinedImageSampler,
			DescriptorCount = 1,
			DstArrayElement = 0,
			ImageInfo = new[] {
				new DescriptorImageInfo {
					Sampler = sampler,
					ImageView = imageView,
					ImageLayout = ImageLayout.ShaderReadOnlyOptimal
				}
			}
		};
		_device.UpdateDescriptorSets(writeBufferInfo, writeImageInfo);
		return set;
	}
}