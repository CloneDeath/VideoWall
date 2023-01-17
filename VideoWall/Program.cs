using System;
using VideoWall;

try {
    var app = new HelloTriangleApplication();
    app.Run();
}
catch (Exception ex) {
    Console.WriteLine(ex);
    throw;
}