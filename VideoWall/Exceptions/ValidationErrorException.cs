using System;

namespace VideoWall.Exceptions; 

public class ValidationErrorException : Exception {
	public ValidationErrorException(string message) : base(message) { }
}