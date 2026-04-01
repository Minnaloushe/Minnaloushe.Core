# Copilot Instructions

## General Guidelines
- When asked to add or refine logging use auto-generated partial methods with LoggerMessage attribute; create new class if not existed with name [BaseClassName]Logger; accessibility is 'internal'; methods should be declared as extensions over ILogger<[BaseClassName]> if possible, use ILogger as a fallback option if the base class does not have a generic logger introduced; file name pattern is [BaseClassName].logger.cs; don't be confused about compilation errors saying that partial method is not implemented, autogeneration might take time to respond.
- Do not generate code that swallows exceptions; add rethrow and notify the developer instead.
- Never use dynamic type unless explicitly requested.
- When declaring collection parameter perfer IEnumerable<T>
- When returning collection from method prefer IReadOnlyCollection<T> or IReadOnlyList<T> if order is important; use IEnumerable<T> as a fallback option if the collection is expected to be lazily evaluated.

## Test Projects
- Use NUnit, Moq, AwesomeAssertions (successor of FluentAssertions) for unit tests.
- Use extension TaskCompletionSourceExtensions.WaitAsync instead of tcs.Task.WaitAsync(TimeSpan).
- Never use Bitnami images to set up Docker containers for tests.
- When naming test methods, use MethodOrSutName_WhenAction_ThenExpectedResult pattern.
- Use Arrange-Act-Assert pattern in test methods; separate sections with empty lines and comments.
- Test fixture contents should be placed in following order: constants, fields, properties, setups and teardowns, helper methods and then test methods; All parts wrapped into '#region'. Test methods themselves should not be wrapped into the '#region'. If region is empty it should be omitted.
- Do not create mock for ILogger<T>, use NullLogger<T>.Instance instead, unless tests require logger invocation checks
- When test contains multiple assertions use AssertionScope to ensure that all assertions are invoked