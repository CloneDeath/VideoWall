using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace VideoWall; 

public struct Vertex {
	public Vector2D<float> Position;
	public Vector3D<float> Color;

	public static VertexInputBindingDescription GetBindingDescription() {
		return new VertexInputBindingDescription {
			Binding = 0,
			Stride = (uint)Unsafe.SizeOf<Vertex>(),
			InputRate = VertexInputRate.Vertex
		};
	}

	public static VertexInputAttributeDescription[] GetAttributeDescriptions() {
		return new[] {
			new VertexInputAttributeDescription {
				Binding = 0,
				Location = 0,
				Format = Format.R32G32Sfloat,
				Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Position))
			},
			new VertexInputAttributeDescription {
				Binding = 0,
				Location = 1,
				Format = Format.R32G32B32Sfloat,
				Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Color))
			}
		};
	}
}