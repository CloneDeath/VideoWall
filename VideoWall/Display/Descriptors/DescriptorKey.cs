using Silk.NET.Vulkan;

namespace VideoWall.Display.Descriptors; 

public struct DescriptorKey {
	public uint FrameIndex;
	public Buffer UBO;
	public ImageView ImageView;
}