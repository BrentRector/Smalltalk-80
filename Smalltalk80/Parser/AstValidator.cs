namespace Smalltalk80.Parser;

/// <summary>
/// Validates parsed AST nodes and provides semantic analysis.
/// Helps catch errors early and ensures code correctness.
/// </summary>
public static class AstValidator
{
    /// <summary>
    /// Validation result containing errors and warnings.
    /// </summary>
    public record ValidationResult(
        bool IsValid,
        IReadOnlyList<ValidationError> Errors,
        IReadOnlyList<ValidationWarning> Warnings
    )
    {
        /// <summary>
        /// Gets a pre-configured ValidationResult instance representing successful validation with no errors or warnings.
        /// This is a convenience property for the common case of successful validation.
        /// </summary>
        public static ValidationResult Success => new(true, Array.Empty<ValidationError>(), Array.Empty<ValidationWarning>());
    }

    /// <summary>
    /// Represents a validation error.
    /// </summary>
    public record ValidationError(string Message, string? Context = null);

    /// <summary>
    /// Represents a validation warning.
    /// </summary>
    public record ValidationWarning(string Message, string? Context = null);

    /// <summary>
    /// Validate a method definition.
    /// </summary>
    /// <param name="method">The method to validate.</param>
    /// <param name="className">The target class name.</param>
    /// <param name="isClassSide">Whether this is a class-side method.</param>
    /// <returns>Validation result.</returns>
    public static ValidationResult ValidateMethod(MethodDefinitionNode method, string className, bool isClassSide = false)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Validate selector format
        if (string.IsNullOrWhiteSpace(method.Selector))
        {
            errors.Add(new ValidationError("Method selector cannot be empty"));
        }
        else if (!IsValidSelector(method.Selector))
        {
            errors.Add(new ValidationError($"Invalid selector format: {method.Selector}"));
        }

        // Validate argument names
        var argumentNames = new HashSet<string>();
        foreach (var arg in method.Arguments)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                errors.Add(new ValidationError("Argument name cannot be empty"));
            }
            else if (!IsValidIdentifier(arg))
            {
                errors.Add(new ValidationError($"Invalid argument name: {arg}"));
            }
            else if (!argumentNames.Add(arg))
            {
                errors.Add(new ValidationError($"Duplicate argument name: {arg}"));
            }
        }

        // Validate temporaries
        var tempNames = new HashSet<string>(argumentNames);
        foreach (var temp in method.Body.Temporaries)
        {
            if (string.IsNullOrWhiteSpace(temp))
            {
                errors.Add(new ValidationError("Temporary variable name cannot be empty"));
            }
            else if (!IsValidIdentifier(temp))
            {
                errors.Add(new ValidationError($"Invalid temporary variable name: {temp}"));
            }
            else if (!tempNames.Add(temp))
            {
                errors.Add(new ValidationError($"Duplicate variable name: {temp}"));
            }
        }

        // Validate primitive declaration
        if (method.HasPrimitive)
        {
            if (method.PrimitiveIndex < 1 || method.PrimitiveIndex > 255)
            {
                errors.Add(new ValidationError($"Invalid primitive index: {method.PrimitiveIndex}"));
            }
        }

        // Validate method body
        var bodyResult = ValidateSequence(method.Body, tempNames, className, isClassSide);
        errors.AddRange(bodyResult.Errors);
        warnings.AddRange(bodyResult.Warnings);

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Validate a sequence of statements.
    /// </summary>
    /// <param name="sequence">The sequence to validate.</param>
    /// <param name="availableVariables">Available variable names in scope.</param>
    /// <param name="className">The current class context.</param>
    /// <param name="isClassSide">Whether we're in a class-side context.</param>
    /// <returns>Validation result.</returns>
    public static ValidationResult ValidateSequence(SequenceNode sequence, HashSet<string> availableVariables,
        string? className = null, bool isClassSide = false)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Add instance variables to available variables
        if (className != null)
        {
            var classInfo = ClassRegistry.Instance.GetClassInfo(className);
            if (classInfo != null && !isClassSide)
            {
                var allInstanceVars = ClassRegistry.Instance.GetAllInstanceVariables(className);
                foreach (var instVar in allInstanceVars)
                {
                    availableVariables.Add(instVar);
                }
            }
        }

        // Validate each statement
        foreach (var statement in sequence.Statements)
        {
            var stmtResult = ValidateStatement(statement, availableVariables, className, isClassSide);
            errors.AddRange(stmtResult.Errors);
            warnings.AddRange(stmtResult.Warnings);
        }

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Validate a statement.
    /// </summary>
    private static ValidationResult ValidateStatement(StatementNode statement, HashSet<string> availableVariables,
        string? className, bool isClassSide)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        switch (statement)
        {
            case ExpressionStatement exprStmt:
                var exprResult = ValidateExpression(exprStmt.Expression, availableVariables, className, isClassSide);
                errors.AddRange(exprResult.Errors);
                warnings.AddRange(exprResult.Warnings);
                break;

            case AnswerStatement answerStmt:
                var answerResult = ValidateExpression(answerStmt.Expression, availableVariables, className, isClassSide);
                errors.AddRange(answerResult.Errors);
                warnings.AddRange(answerResult.Warnings);
                break;

            case PrimitiveStatement primitiveStmt:
                if (primitiveStmt.PrimitiveIndex < 1 || primitiveStmt.PrimitiveIndex > 255)
                {
                    errors.Add(new ValidationError($"Invalid primitive index: {primitiveStmt.PrimitiveIndex}"));
                }
                break;
        }

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Validate an expression.
    /// </summary>
    private static ValidationResult ValidateExpression(ExpressionNode expression, HashSet<string> availableVariables,
        string? className, bool isClassSide)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        switch (expression)
        {
            case VariableExpression varExpr:
                if (!availableVariables.Contains(varExpr.Name) && !IsPseudoVariable(varExpr.Name))
                {
                    errors.Add(new ValidationError($"Undefined variable: {varExpr.Name}"));
                }
                break;

            case AssignmentExpression assignExpr:
                if (!availableVariables.Contains(assignExpr.Variable))
                {
                    errors.Add(new ValidationError($"Cannot assign to undefined variable: {assignExpr.Variable}"));
                }
                else if (IsPseudoVariable(assignExpr.Variable))
                {
                    errors.Add(new ValidationError($"Cannot assign to pseudo variable: {assignExpr.Variable}"));
                }

                var valueResult = ValidateExpression(assignExpr.Value, availableVariables, className, isClassSide);
                errors.AddRange(valueResult.Errors);
                warnings.AddRange(valueResult.Warnings);
                break;

            case UnarySendExpression unaryExpr:
                var receiverResult = ValidateExpression(unaryExpr.Receiver, availableVariables, className, isClassSide);
                errors.AddRange(receiverResult.Errors);
                warnings.AddRange(receiverResult.Warnings);
                break;

            case BinarySendExpression binaryExpr:
                var binReceiverResult = ValidateExpression(binaryExpr.Receiver, availableVariables, className, isClassSide);
                var binArgResult = ValidateExpression(binaryExpr.Argument, availableVariables, className, isClassSide);
                errors.AddRange(binReceiverResult.Errors);
                errors.AddRange(binArgResult.Errors);
                warnings.AddRange(binReceiverResult.Warnings);
                warnings.AddRange(binArgResult.Warnings);
                break;

            case KeywordSendExpression kwExpr:
                var kwReceiverResult = ValidateExpression(kwExpr.Receiver, availableVariables, className, isClassSide);
                errors.AddRange(kwReceiverResult.Errors);
                warnings.AddRange(kwReceiverResult.Warnings);

                foreach (var arg in kwExpr.Arguments)
                {
                    var argResult = ValidateExpression(arg, availableVariables, className, isClassSide);
                    errors.AddRange(argResult.Errors);
                    warnings.AddRange(argResult.Warnings);
                }
                break;

            case BlockExpression blockExpr:
                var blockVars = new HashSet<string>(availableVariables);
                foreach (var param in blockExpr.Parameters)
                {
                    if (!IsValidIdentifier(param))
                    {
                        errors.Add(new ValidationError($"Invalid block parameter name: {param}"));
                    }
                    else if (!blockVars.Add(param))
                    {
                        errors.Add(new ValidationError($"Duplicate block parameter: {param}"));
                    }
                }

                var blockResult = ValidateSequence(blockExpr.Body, blockVars, className, isClassSide);
                errors.AddRange(blockResult.Errors);
                warnings.AddRange(blockResult.Warnings);
                break;

            case CascadeExpression cascadeExpr:
                var cascadeReceiverResult = ValidateExpression(cascadeExpr.Receiver, availableVariables, className, isClassSide);
                errors.AddRange(cascadeReceiverResult.Errors);
                warnings.AddRange(cascadeReceiverResult.Warnings);

                foreach (var message in cascadeExpr.Messages)
                {
                    var msgResult = ValidateExpression(message, availableVariables, className, isClassSide);
                    errors.AddRange(msgResult.Errors);
                    warnings.AddRange(msgResult.Warnings);
                }
                break;
        }

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Check if a string is a valid Smalltalk identifier.
    /// </summary>
    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    /// <summary>
    /// Check if a string is a valid Smalltalk selector.
    /// </summary>
    private static bool IsValidSelector(string selector)
    {
        if (string.IsNullOrEmpty(selector))
            return false;

        // Binary selector
        if (selector.All(c => "+-*/\\~<>=@%|&?!".Contains(c)))
            return selector.Length <= 2;

        // Keyword selector
        if (selector.Contains(':'))
        {
            var parts = selector.Split(':');
            if (parts.Length < 2 || !string.IsNullOrEmpty(parts[^1]))
                return false;

            return parts.Take(parts.Length - 1).All(IsValidIdentifier);
        }

        // Unary selector
        return IsValidIdentifier(selector);
    }

    /// <summary>
    /// Check if a variable name is a pseudo variable.
    /// </summary>
    private static bool IsPseudoVariable(string name)
    {
        return name is "self" or "super" or "nil" or "true" or "false" or "thisContext";
    }
}
