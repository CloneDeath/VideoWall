using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using VideoWall.Server.Controllers;

namespace VideoWall.Server; 

public class VideoServer {
	private readonly IVideoWall _wall;
	private WebApplication? _app;

	public VideoServer(IVideoWall wall) {
		_wall = wall;
	}
	
	public Task Start() {
		var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
			EnvironmentName = "Development"
		});
		
		builder.Services.AddControllers();
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen();
		builder.Services.AddSingleton(_wall);

		_app = builder.Build();
		if (_app.Environment.IsDevelopment()) {
			_app.UseDeveloperExceptionPage();
			_app.UseSwagger();
			_app.UseSwaggerUI();
		}

		_app.UseHttpsRedirection();
		// _app.UseDefaultFiles();
		_app.UseStaticFiles(new StaticFileOptions
		{
			FileProvider = new ManifestEmbeddedFileProvider(GetType().Assembly, "resources"),
			RequestPath = ""
		});

		_app.UseAuthorization();
		_app.MapControllers();
		return _app.RunAsync();
	}

	public ValueTask Stop() {
		return _app?.DisposeAsync() ?? ValueTask.CompletedTask;
	}
}