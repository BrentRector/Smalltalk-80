using Smalltalk80;

namespace Smalltalk80.ByteCodes.Return;

/// <summary>
///     Returns the top of stack either from a method or from a block.
/// </summary>
public sealed class ReturnTopOperation : IBytecodeOperation
{
    private readonly bool isBlock;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReturnTopOperation"/> class.
    /// </summary>
    /// <param name="isBlock">Whether the return originates from a block.</param>
    public ReturnTopOperation(bool isBlock) => this.isBlock = isBlock;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        var value = ctx.Stack.Count > 0 ? ctx.Stack.Pop() : ctx.Receiver;
        if (isBlock)
        {
            // Non-local return unwinds to the block's caller.
            if (ctx is global::BlockContext blk && blk.Sender != blk.HomeContext)
            {
                throw new global::NonLocalReturnException(value, blk.HomeContext.Sender);
            }
        }

        interpreter.ReturnFrom(value!);
    }
}
