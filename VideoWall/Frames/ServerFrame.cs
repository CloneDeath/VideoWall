using System;
using Silk.NET.Maths;
using VideoWall.Server.Controllers;

namespace VideoWall.Frames; 

public class ServerFrame : IFrame {
	private readonly DisplayFrame _frame;

	public ServerFrame(DisplayFrame frame) {
		_frame = frame;
	}

	public Guid Id => _frame.Id;
	public Location Location {
		get => new() {
			X = _frame.Position.X,
			Y = _frame.Position.Y,
			Width = _frame.Size.X,
			Height = _frame.Size.Y
		};
		set {
			_frame.Position = new Vector2D<float>(value.X, value.Y);
			_frame.Size = new Vector2D<float>(value.Width, value.Height);
		}
	}
}