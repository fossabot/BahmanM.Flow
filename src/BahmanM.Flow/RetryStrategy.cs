namespace BahmanM.Flow;

internal class RetryStrategy : IBehaviourStrategy
{
    private readonly Type[] _nonRetryableExceptions;
    private readonly int _maxAttempts;

    public RetryStrategy(int maxAttempts, params Type[] nonRetryableExceptions)
    {
        _nonRetryableExceptions = nonRetryableExceptions;
        _maxAttempts = maxAttempts > 0
            ? maxAttempts
            : throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be a positive integer.");
    }
    
    public RetryStrategy(int maxAttempts) : this(maxAttempts, [typeof(TimeoutException)])
    {
    }

    #region Pass-through Implementations

    public IFlow<T> ApplyTo<T>(SucceededNode<T> node) => node;
    public IFlow<T> ApplyTo<T>(FailedNode<T> node) => node;

    public IFlow<T> ApplyTo<T>(DoOnSuccessNode<T> node) =>
        node with { Upstream = ((IFlowNode<T>)node.Upstream).Apply(this) };

    public IFlow<T> ApplyTo<T>(AsyncDoOnSuccessNode<T> node) =>
        node with { Upstream = ((IFlowNode<T>)node.Upstream).Apply(this) };

    public IFlow<T> ApplyTo<T>(DoOnFailureNode<T> node) =>
        node with { Upstream = ((IFlowNode<T>)node.Upstream).Apply(this) };

    public IFlow<T> ApplyTo<T>(AsyncDoOnFailureNode<T> node) =>
        node with { Upstream = ((IFlowNode<T>)node.Upstream).Apply(this) };

    public IFlow<TOut> ApplyTo<TIn, TOut>(SelectNode<TIn, TOut> node) =>
        node with { Upstream = ((IFlowNode<TIn>)node.Upstream).Apply(this) };

    public IFlow<TOut> ApplyTo<TIn, TOut>(AsyncSelectNode<TIn, TOut> node) =>
        node with { Upstream = ((IFlowNode<TIn>)node.Upstream).Apply(this) };

    #endregion

    #region Rewriting Implementations
    
    public IFlow<T> ApplyTo<T>(CreateNode<T> node)
    {
        Func<T> newOperation = () =>
        {
            Exception lastException = null!;
            for (var i = 0; i < _maxAttempts; i++)
            {
                try
                {
                    return node.Operation();
                }
                catch (Exception ex)
                {
                    if (_nonRetryableExceptions.Contains(ex.GetType())) 
                        throw;
                    lastException = ex;
                }
            }
            throw lastException!;
        };
        return new CreateNode<T>(newOperation);
    }

    public IFlow<TOut> ApplyTo<TIn, TOut>(ChainNode<TIn, TOut> node)
    {
        Func<TIn, IFlow<TOut>> newOperation = (value) =>
            ((IFlowNode<TOut>)node.Operation(value)).Apply(this);
        return node with { Operation = newOperation };
    }

    public IFlow<T> ApplyTo<T>(AsyncCreateNode<T> node)
    {
        Func<Task<T>> newOperation = async () =>
        {
            Exception lastException = null!;
            for (var i = 0; i < _maxAttempts; i++)
            {
                try
                {
                    return await node.Operation();
                }
                catch (Exception ex)
                {
                    if (_nonRetryableExceptions.Contains(ex.GetType())) 
                        throw;
                    lastException = ex;
                }
            }
            throw lastException!;
        };
        return new AsyncCreateNode<T>(newOperation);
    }

    public IFlow<TOut> ApplyTo<TIn, TOut>(AsyncChainNode<TIn, TOut> node)
    {
        Func<TIn, Task<IFlow<TOut>>> newOperation = async (value) =>
            ((IFlowNode<TOut>)await node.Operation(value)).Apply(this);
        return node with { Operation = newOperation };
    }

    #endregion
}
