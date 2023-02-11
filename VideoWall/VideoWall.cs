using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VideoWall.Display;
using VideoWall.Frames;
using VideoWall.Server.Controllers;

namespace VideoWall; 

public class VideoWall : IVideoWall, IDisposable {
	private readonly VideoWallApplication _app;
	private readonly List<DisplayFrame> _frames = new();

	public IEnumerable<IFrame> Frames => _frames.Select(f => new ServerFrame(f));

	public VideoWall() {
		_app = new VideoWallApplication();
	}
	~VideoWall() => Dispose();
	public void Dispose() {
		_app.Dispose();
		GC.SuppressFinalize(this);
	}

	public void AddFrame(DisplayFrame frame) {
		_frames.Add(frame);
		_app.AddEntity(frame);
	}

	public void Init() => _app.Init();

	public Task Run() {
		return Task.Run(() => _app.Run());
	}
}