using Smalltalk80.Validator.Core;

namespace Smalltalk80.Validator.Engine;

public class ValidatorPipeline
{
    private readonly List<IValidator> _validators = [];

    public ValidatorPipeline AddValidator(IValidator validator)
    {
        _validators.Add(validator);
        return this;
    }

    public ValidatorRunner Build() => new ValidatorRunner(_validators);
}
