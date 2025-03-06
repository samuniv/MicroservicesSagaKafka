using System;

namespace OrderService.Domain.Exceptions
{
    public class InvalidOrderStateException : Exception
    {
        public InvalidOrderStateException(string message) : base(message)
        {
        }

        public InvalidOrderStateException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
} 