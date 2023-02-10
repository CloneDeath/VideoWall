using System;

namespace VideoWall.Display.Exceptions; 

public class ValidationErrorException : Exception {
	public ValidationErrorException(string message) : base(message) { }
}