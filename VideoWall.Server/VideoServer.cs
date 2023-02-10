using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VideoWall.Server.Controllers;

namespace VideoWall.Server; 

public class VideoServer {
	private readonly IVideoWall _wall;

	public VideoServer(IVideoWall wall) {
		_wall = wall;
	}
	
	public Task Start() {
		var builder = WebApplication.CreateBuilder();

		builder.Services.AddControllers();
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen();
		builder.Services.AddSingleton(_wall);

		var app = builder.Build();

		//if (app.Environment.IsDevelopment()) {
			app.UseSwagger();
			app.UseSwaggerUI();
		//}

		app.UseHttpsRedirection();
		app.UseAuthorization();
		app.MapControllers();
		return app.RunAsync();
	}
}