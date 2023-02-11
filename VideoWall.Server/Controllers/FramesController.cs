using Microsoft.AspNetCore.Mvc;

namespace VideoWall.Server.Controllers;

[ApiController]
[Route("VideoWall/[controller]")]
public class FramesController : ControllerBase {
	private readonly IVideoWall _wall;

	public FramesController(IVideoWall wall) {
		_wall = wall;
	}

	[HttpGet]
	public IEnumerable<IFrame> AllFrames() => _wall.Frames;

	[HttpGet("{id:guid}")]
	public IFrame? GetFrame(Guid id) => _wall.Frames.FirstOrDefault(f => f.Id == id);

	[HttpPut("{id:guid}")]
	public void SetFrameLocation(Guid id, [FromBody] Location location) {
		var frame = _wall.Frames.First(f => f.Id == id);
		frame.Location = location;
	}
}