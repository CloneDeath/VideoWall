namespace VideoWall; 

public class QueueFamilyIndices {
	public uint? GraphicsFamily { get; set; }
	public uint? PresentFamily { get; set; }

	public bool IsComplete() {
		return GraphicsFamily.HasValue && PresentFamily.HasValue;
	}
}