using System.Collections.Generic;
using SilkNetConvenience.Descriptors;

namespace VideoWall.Display.Descriptors; 

public class DescriptorCollection<T> where T : notnull {
	private readonly VulkanDescriptorPool _descriptorPool;
	private readonly VulkanDescriptorSetLayout _layout;
	private readonly Dictionary<T, VulkanDescriptorSet> _descriptorSets = new();

	public DescriptorCollection(VulkanDescriptorPool descriptorPool, VulkanDescriptorSetLayout layout) {
		_descriptorPool = descriptorPool;
		_layout = layout;
	}

	public VulkanDescriptorSet GetSet(T key) {
		if (_descriptorSets.TryGetValue(key, out var set)) return set;

		var newSet = _descriptorPool.AllocateDescriptorSet(_layout);
		_descriptorSets[key] = newSet;
		return newSet;
	}
}