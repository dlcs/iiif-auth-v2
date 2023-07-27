namespace IIIFAuth2.API.Models.Result;

/// <summary>
/// Generic result class that represents an object and success/fail status
/// </summary>
public class ResultStatus<T>
    where T : class
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// The associated value.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultStatus{T}"/> class.
    /// </summary>
    /// <param name="success">if set to <c>true</c> [success].</param>
    public ResultStatus(bool success)
    {
        Success = success;
        Value = default!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultStatus{T}"/> class.
    /// </summary>
    /// <param name="success">if set to <c>true</c> [success].</param>
    /// <param name="value">The value.</param>
    public ResultStatus(bool success, T? value)
    {
        Success = success;
        Value = value;
    }

    /// <summary>
    /// Creates an unsuccessful result with specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public static ResultStatus<T> Unsuccessful(T? value = null) => new(false, value);

    /// <summary>
    /// Creates a successful result with specified value
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public static ResultStatus<T> Successful(T value) => new(true, value);
}