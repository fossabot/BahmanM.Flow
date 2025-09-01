using System.Reflection;
using BahmanM.Flow.Ast;
using BahmanM.Flow.Execution.Continuations;
using BahmanM.Flow.Execution.Continuations.Resource;

namespace BahmanM.Flow.Execution.Planning;

internal interface IWithResourcePlan<T>
{
    IDisposable Acquire();
    INode<T> Use(IDisposable resource);
    IContinuation<T> CreateDisposeCont(IDisposable resource);
}

internal static class WithResourcePlanner
{
    private static readonly Type WithResourceDef = typeof(BahmanM.Flow.Ast.Resource.WithResource<,>);

    internal static bool TryCreate<T>(INode<T> node, out IWithResourcePlan<T> plan)
    {
        var t = node.GetType();
        if (!t.IsGenericType || t.GetGenericTypeDefinition() != WithResourceDef)
        {
            plan = null!;
            return false;
        }

        var args = t.GetGenericArguments();
        var tResource = args[0];
        var method = typeof(WithResourcePlanner).GetMethod(nameof(CreateGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;
        var gmethod = method.MakeGenericMethod(tResource, typeof(T));
        plan = (IWithResourcePlan<T>)gmethod.Invoke(null, new object[] { node })!;
        return true;
    }

    private static IWithResourcePlan<T> CreateGeneric<TResource, T>(BahmanM.Flow.Ast.Resource.WithResource<TResource, T> wr)
        where TResource : IDisposable
        => new Plan<TResource, T>(wr);

    private sealed class Plan<TResource, T>(BahmanM.Flow.Ast.Resource.WithResource<TResource, T> wr) : IWithResourcePlan<T>
        where TResource : IDisposable
    {
        public IDisposable Acquire() => wr.Acquire();

        public INode<T> Use(IDisposable resource) => (INode<T>)wr.Use((TResource)resource);

        public IContinuation<T> CreateDisposeCont(IDisposable resource) => new DisposeCont<TResource, T>((TResource)resource);
    }
}
