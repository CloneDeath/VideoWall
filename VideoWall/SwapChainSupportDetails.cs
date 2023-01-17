using System;
using Silk.NET.Vulkan;

namespace VideoWall; 

public class SwapChainSupportDetails {
	public SurfaceCapabilitiesKHR Capabilities;
	public SurfaceFormatKHR[] Formats = Array.Empty<SurfaceFormatKHR>();
	public PresentModeKHR[] PresentModes = Array.Empty<PresentModeKHR>();
}