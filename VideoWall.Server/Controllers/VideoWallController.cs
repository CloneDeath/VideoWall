using Microsoft.AspNetCore.Mvc;

namespace VideoWall.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class VideoWallController : ControllerBase {
	private readonly IVideoWall _wall;

	public VideoWallController(IVideoWall wall) {
		_wall = wall;
	}

	[HttpGet("[action]", Name = "Frames")]
	public IEnumerable<IFrame> Frames() => _wall.Frames;
}