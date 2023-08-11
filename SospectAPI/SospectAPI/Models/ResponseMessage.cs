using System;
namespace SospectAPI.Models
{
    public class ResponseMessage
    {
        public bool IsSuccess { get; set; }
        public object? Data { get; set; }
        public string? Message { get; set; }
    }
}

