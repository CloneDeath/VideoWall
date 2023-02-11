using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace VideoWall.Server.Controllers; 

public class FrameData {
	private readonly IFrame _frame;

	public FrameData(IFrame frame) {
		_frame = frame;
	}

	public Guid Id => _frame.Id;
	public Location Location => _frame.Location;
	public string Image => _frame.Image.ToBase64String(PngFormat.Instance);
}