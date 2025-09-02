# Design Rationale

This section documents the key architectural decisions that shape the Flow API, focusing on the "why" behind the design.

# 1. The Core Philosophy: Recipe & Chef

The central architectural pattern is a strict separation of concerns:

*   **`IFlow<T>` is the Recipe:** It is a purely declarative, **immutable** data structure representing a sequence of operations. It is an Abstract Syntax Tree (AST) that defines *what* to do, not *how* to do it. Each operation in a `Flow` (e.g., `SucceededFlow`, `SelectFlow`) is a node in this AST.

*   **`FlowEngine` is the Chef:** It is the interpreter that executes the `IFlow<T>` AST using a stack‑safe trampoline, continuations, and operator‑specific planning. For a detailed, practical explanation of how execution works phase‑by‑phase, see [Trampoline Execution Engine](./Trampoline-Execution-Engine.md).

*   **Operators are Pure Functions:** All extension methods in `FlowExtensions` (e.g., `.Chain()`, `.Select()`) are pure functions. They take a flow as input and return a **new, decorated `IFlow<T>` instance**, never modifying the original. This guarantees that flow declarations are immutable, reusable, and safe to share.

### 1a. The Philosophy: Pragmatism over Purity

While Flow is heavily inspired by functional programming concepts, it is not a strict FP framework. 

The primary goal is to provide an intuitive, discoverable, and productive API for the typical C# developer.

This means Flow will always favour a design that is **pragmatic and familiar** over one that is theoretically pure but abstract. 

A good example of this is the approach to concurrency:

*   Flow provides `Flow.All` and `Flow.Any`, which are direct conceptual mirrors of `Task.WhenAll` and `Task.WhenAny`. A C# developer will instantly understand what these do.

*   A purist approach might have offered a single, more powerful `Traverse` function. However, that would require users to understand concepts like Applicative Functors, creating a steep learning curve.

The design choice is to provide specific, named solutions to common problems rather than a single, generic tool that requires academic knowledge to use. 

This principle of pragmatism guides the entire API design.

(For those interested, a more detailed mapping of Flow concepts to their functional counterparts can be found in [Notes for FP Developers](./ForFPDevelopers.md).)

# 2. `FlowExecutionOptions`: Decoupling Execution from Declaration

All parameters that control a specific execution run (the "how-to-run" instructions) are contained within the `FlowExecutionOptions` object. 

This includes the `CancellationToken` and the `IFlowExecutionObserver`. 

This approach keeps the `FlowEngine.ExecuteAsync` signature stable and makes the API extensible, as new execution-time options can be added without breaking changes.

# 3. The Observer Pattern for Diagnostics

To provide visibility into the engine's execution, Flow is designed with a decoupled observer pattern rather than baking diagnostics directly in.

*   **`IFlowExecutionObserver`:** This interface defines a contract for observing the engine's lifecycle. 

_It is designed to be implemented by users to bridge `Flow` events to their chosen diagnostic or logging framework._

*   **Multi-Method Interface:** The interface uses specific, discoverable methods (`OnOperationStarted`, `OnFlowSucceeded`, etc.) with default implementations. 

_This provides the best of both worlds: it's easy for users to implement (they only override what they need), and it's extensible for Flow (new methods can be added without breaking existing implementations)._

*   **Interface-Based Events:** The events passed to the observer are defined by a hierarchy of interfaces (`IFlowEvent`, `IOperationEvent`, etc.). 

_This allows for flexible, type-safe pattern matching in observer implementations while decoupling the observer from the concrete event implementation classes._

# 4. Extensible Enumerations: The `FlowOperationTypes` Pattern

To identify the type of operation in a diagnostic event, Flow uses a `string` property (`IOperationEvent.OperationType`).

To provide compile-time safety for built-in operations, Flow additionally provides a `public static class FlowOperationTypes` containing `const string` definitions. 

This is a standard .NET library pattern that provides the safety of an `enum` for known types and the extensibility of a `string` for user-defined custom operations.

# 5. Strongly-Typed IDs

Instead of using primitive `string` types for flow and operation identifiers, Flow uses dedicated `FlowId` and `OperationId` classes. 

This is the best practice that:
*   Improves type safety throughout the API.
*   Encapsulates the ID generation logic (`FlowId.Generate()`).
*   Makes the code more self-documenting.

# 6. The `Failure` Model

A `Failure<T>` in the `Outcome<T>` model always contains an `Exception`. 

This was a deliberate design choice to align with standard .NET idioms. 

"Business-level" failures (e.g., validation errors) should be modeled as a type of `Success<T>` (e.g., `Success<ValidationResult>`), while the `Failure` path is reserved for true, exceptional circumstances that disrupt the normal flow of a computation.

# 7. Resource Management Patterns

The Flow library provides a single, clear pattern for managing resources.

*   **`Flow.WithResource`:** This factory is used for **isolated, operation-scoped resources**. The mandatory `acquire` function makes it explicit that a new resource is created for the scope of the `use` function. The `where TResource : IDisposable` constraint ensures that the resource is properly disposed of by the engine, mirroring the behavior of a standard C# `using` block. It is the correct tool for encapsulated resources, like an `HttpClient` for a single API call.

*   **Shared Resources (Deferred):** The problem of managing resources that must be shared across multiple, independent operations (e.g., a `DbTransaction`) is a more advanced use case. The exploration of a `FlowEnvironment` (`Reader` monad) pattern to solve this has been deferred to a future work item (`dx006`) to keep the core API lean and focused.

# 8. The Behaviour System: AST Rewriting

Operators like `.WithRetry()` and `.WithTimeout()` are fundamentally different from simple operators like `.Select()`. They don't just transform a value; they alter the **execution strategy** of a preceding operation. A naive implementation that simply re-executes the entire upstream flow is dangerous, as it can lead to the re-execution of unintended side-effects (e.g., sending an email multiple times).

To solve this problem correctly while keeping the `FlowEngine` simple, behaviours are implemented using an **AST (Abstract Syntax Tree) Rewriting** approach.

*   **Operators as Rewriters:** When an operator like `.WithRetry()` is called, it does not simply wrap the existing flow in a new node. Instead, it inspects the final node of the upstream flow and returns a *new, rewritten flow* with the behaviour's logic baked directly into the failable operation.

*   **The Visitor Pattern for Rewriting:** This rewriting is implemented internally using a second Visitor pattern.
    1.  An exhaustive `internal interface IBehaviourStrategy` defines the contract for rewriting, with a method for every `IFlowNode` type. This ensures compile-time safety when new node types are added.
    2.  A concrete implementation, like `RetryStrategy`, contains the specific logic for how to rewrite failable nodes (`CreateNode`, `ChainNode`) to incorporate retry logic.
    3.  The public `.WithRetry()` extension method creates a `RetryStrategy` and passes it to the flow's internal `Apply` method, which triggers the Visitor dispatch.

This design keeps the `FlowEngine` completely ignorant of complex behaviours, fulfilling its role as a simple, dumb interpreter.

### 8a. The Pragmatic API for Behaviours

A key design decision was how to handle the application of a behaviour to different types of operations. Flow takes a pragmatic approach, distinguishing between behaviours that alter execution strategy and those that apply custom logic.

*   **Execution Behaviours (`.WithRetry()`, `.WithTimeout()`): A Silent No-Op**

    These built-in behaviours are designed to modify failable operations like `Create` or `Chain`. Applying a retry or a timeout to a pure, non-failable transformation like `Select` is a logical contradiction.

    While Flow could throw a runtime `InvalidOperationException` in this case, the chosen behavior is to make this a **silent no-op**. The operator will simply return an equivalent flow.

    This is a conscious trade-off that prioritizes a frictionless developer experience. 

    A user might refactor a `Chain` into a `Select` (because the operation was made pure), and they should not be punished by having their code suddenly throw an exception because they forgot to remove a now-redundant `.WithRetry()` call. 

    Flow favors robustness, and treating the invalid application as a no-op is the most robust and least surprising behavior.

*   **Custom Behaviours (`.WithBehaviour()`): Universal Application**

    The generic `.WithBehaviour()` operator, in contrast, is designed for maximum flexibility. 
    
    It allows users to create any kind of cross-cutting concern, such as logging, auditing, or state tracking. 
    
    These concerns are often relevant to *every* step in a flow, not just the failable ones.

    Therefore, `.WithBehaviour()` can be applied to *any* node in the flow. 

    It does not perform a no-op on pure transformations, allowing developers to build powerful, universal behaviours that can observe or interact with the entire pipeline.

This two-pronged approach provides both safety and predictability for the built-in execution modifiers and maximum power and flexibility for user-defined custom behaviours.

# 9. The `Validate` Operator and Value-Introspection

### 9a. Introduction: A New Class of Operator

The introduction of `.Validate()` marks a new category of operator in the library. While most operators are "State-Reactive," `.Validate()` is "Value-Introspective."

### 9b. State-Reactive vs. Value-Introspective Operators

*   **State-Reactive:** These operators react to the *state* of the flow (`Success` or `Failure`) without needing to inspect the value. `Chain`, `Recover`, and `DoOn...` are the primary examples.
*   **Value-Introspective:** These operators "look inside" a `Success<T>` outcome to make decisions based on the value `T` itself. `.Validate()` is the first and most important operator in this category.

### 9c. Parallels in Functional Programming

This concept is not unique to `Flow`. In functional libraries like Cats Effect or ZIO, this capability is typically composed from the fundamental `flatMap` primitive or exposed via methods like `.ensure` or `.filterOrFail`. `Flow` makes a deliberate design choice to elevate this common pattern to a named, first-class operator to improve ergonomics, discoverability, and readability for a broader audience.

### 9d. Design Deep Dive: The `Validate` Signature

*   **The Name (`Validate`):** The name `Validate` was chosen because it best describes the developer's *intent* (asserting that the data is in a valid state for the happy path) and encourages positive, declarative predicates (`user.IsAdmin`).
*   **The Exception Factory (`Func<T, Exception>`):** This is a crucial part of the signature. It avoids the performance overhead of creating an exception on every call and, more importantly, allows for rich, **contextual error messages** by giving the factory access to the value that failed validation.

    ```csharp
    // Good: The error message can include the specific, problematic value.
    .Validate(
        name => name.Length > 3,
        name => new ArgumentException($"Username '{name}' is too short.")
    )
    ```

In addition to the synchronous signature above, Flow also exposes asynchronous and cancellable variants to mirror the rest of the API surface. These variants preserve the exact same outcome semantics; they differ only in how the predicate is evaluated:

```
IFlow<T> Validate<T>(Func<T, Task<bool>> predicateAsync, Func<T, Exception> exceptionFactory)
IFlow<T> Validate<T>(Func<T, CancellationToken, Task<bool>> predicateCancellableAsync, Func<T, Exception> exceptionFactory)
```

Use the cancellable overload when you want the predicate’s evaluation to respect the execution token provided to the engine.
