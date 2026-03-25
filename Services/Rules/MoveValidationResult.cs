namespace jeuPoint.Services.Rules;

public sealed class MoveValidationResult
{
    public bool IsValid { get; }

    public string Message { get; }

    private MoveValidationResult(bool isValid, string message)
    {
        IsValid = isValid;
        Message = message;
    }

    public static MoveValidationResult Valid()
    {
        return new MoveValidationResult(true, "OK");
    }

    public static MoveValidationResult Invalid(string message)
    {
        return new MoveValidationResult(false, message);
    }

    public override string ToString()
    {
        return $"MoveValidationResult {{ IsValid = {IsValid}, Message = {Message} }}";
    }
}
