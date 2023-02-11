using Microsoft.AspNetCore.Mvc;

namespace VideoWall.Server.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class VideoWallController : ControllerBase {
	private readonly IVideoWall _wall;

	public VideoWallController(IVideoWall wall) {
		_wall = wall;
	}

	[HttpGet]
	public IEnumerable<IFrame> Frames() => _wall.Frames;
}