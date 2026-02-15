using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.Core.Models
{
    public class Result
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        static public Result Ok() => new Result { Success = true };

        static public Result Fail(string message) => new Result { Success = false, Message = message };
    }

    public class Result<T> : Result
    {
        public T Value { get; init; }
        static public Result<T> Ok(T data) => new Result<T> { Success = true, Value = data };
        static public new Result<T> Fail(string message) => new Result<T> { Success = false, Message = message, Value = default };
    }
}
