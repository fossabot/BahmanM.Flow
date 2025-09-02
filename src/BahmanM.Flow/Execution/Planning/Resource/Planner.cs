using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Continuations.Resource;

namespace BahmanM.Flow.Execution.Planning.Resource;

internal interface IWithResourcePlan<T>
{
    IDisposable Acquire();
    INode<T> Use(IDisposable resource);
    IContinuation<T> CreateDisposeCont(IDisposable resource);
}

internal static class ResourcePlanner
{
    private static readonly Type WithResourceDef = typeof(BahmanM.Flow.Ast.Resource.WithResource<,>);

    internal static bool TryCreate<T>(INode<T> node, out IWithResourcePlan<T> resourcePlan)
    {
        var nodeType = node.GetType();
        if (!nodeType.IsGenericType || nodeType.GetGenericTypeDefinition() != WithResourceDef)
        {
            resourcePlan = null!;
            return false;
        }

        var typeArguments = nodeType.GetGenericArguments();
        var resourceType = typeArguments[0];
        var dispatch = typeof(ResourcePlanner).GetMethod(nameof(CreateGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var genericMethod = dispatch.MakeGenericMethod(resourceType, typeof(T));
        resourcePlan = (IWithResourcePlan<T>)genericMethod.Invoke(null, [node])!;
        return true;
    }

    private static IWithResourcePlan<T> CreateGeneric<TResource, T>(BahmanM.Flow.Ast.Resource.WithResource<TResource, T> withResource)
        where TResource : IDisposable
        => new Plan<TResource, T>(withResource);

    private sealed class Plan<TResource, T>(BahmanM.Flow.Ast.Resource.WithResource<TResource, T> withResource) : IWithResourcePlan<T>
        where TResource : IDisposable
    {
        public IDisposable Acquire() => withResource.Acquire();

        public INode<T> Use(IDisposable resource) => (INode<T>)withResource.Use((TResource)resource);

        public IContinuation<T> CreateDisposeCont(IDisposable resource) => new DisposeCont<TResource, T>((TResource)resource);
    }
}
