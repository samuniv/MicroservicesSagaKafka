namespace OrderService.Domain.Exceptions;

public class OrderDomainException : Exception
{
    public OrderDomainException()
    { }

    public OrderDomainException(string message)
        : base(message)
    { }

    public OrderDomainException(string message, Exception innerException)
        : base(message, innerException)
    { }
}

public class InvalidOrderStateException : OrderDomainException
{
    public InvalidOrderStateException(string message) : base(message)
    { }
}

public class OrderItemValidationException : OrderDomainException
{
    public OrderItemValidationException(string message) : base(message)
    { }
} 