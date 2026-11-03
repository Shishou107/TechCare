namespace API_FixComputerCenter.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string name, object key) : base($"Entity '{name}' with ID ({key}) was not found.") { }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]> { { "General", new[] { message } } };
    }

    public ValidationException(string propertyName, string message) : base(message)
    {
        Errors = new Dictionary<string, string[]> { { propertyName, new[] { message } } };
    }

    public ValidationException(IDictionary<string, string[]> errors) : this()
    {
        Errors = errors;
    }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "You do not have permission to access this resource.") : base(message) { }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Authentication is required to access this resource.") : base(message) { }
}

public class EmailNotVerifiedException : Exception
{
    public string Email { get; }
    public EmailNotVerifiedException(string email, string message = "Tài khoản của bạn chưa được xác thực email. Vui lòng nhập mã xác thực 6 số đã được gửi đến email của bạn.") : base(message)
    {
        Email = email;
    }
}