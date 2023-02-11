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
	public IEnumerable<FrameData> AllFrames() => _wall.Frames.Select(f => new FrameData(f));

	[HttpGet("{id:guid}")]
	public FrameData? GetFrame(Guid id) => AllFrames().FirstOrDefault(f => f.Id == id);

	[HttpPut("{id:guid}")]
	public void SetFrameLocation(Guid id, [FromBody] Location location) {
		var frame = _wall.Frames.First(f => f.Id == id);
		frame.Location = location;
	}
}