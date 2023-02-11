namespace VideoWall.Server.Controllers; 

public interface IFrame {
	public Guid Id { get; }
	public Location Location { get; }
}