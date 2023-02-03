using System;
using Silk.NET.Vulkan;

namespace VideoWall.Display; 

public class SwapchainSupportDetails {
	public SurfaceCapabilitiesKHR Capabilities;
	public SurfaceFormatKHR[] Formats = Array.Empty<SurfaceFormatKHR>();
	public PresentModeKHR[] PresentModes = Array.Empty<PresentModeKHR>();
}