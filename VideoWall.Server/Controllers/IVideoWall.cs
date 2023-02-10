namespace VideoWall.Server.Controllers; 

public interface IVideoWall {
	public IEnumerable<IFrame> Frames { get; }
}