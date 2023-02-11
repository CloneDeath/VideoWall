using System;
using VideoWall.Server.Controllers;

namespace VideoWall.Frames; 

public class ServerFrame : IFrame {
	private readonly DisplayFrame _frame;

	public ServerFrame(DisplayFrame frame) {
		_frame = frame;
	}

	public Guid Id => _frame.Id;
	public Location Location => new() {
		X = _frame.Position.X,
		Y = _frame.Position.Y,
		Width = 1,
		Height = 1
	};
}