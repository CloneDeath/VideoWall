using SixLabors.ImageSharp;

namespace VideoWall.Server.Controllers; 

public interface IFrame {
	public Guid Id { get; }
	public Location Location { get; set; }
	public Image Image { get; } 
}