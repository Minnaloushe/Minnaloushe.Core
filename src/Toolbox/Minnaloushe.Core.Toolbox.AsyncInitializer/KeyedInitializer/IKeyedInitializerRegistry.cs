namespace Minnaloushe.Core.Toolbox.AsyncInitializer.KeyedInitializer;

internal interface IKeyedInitializerRegistry
{
    IReadOnlySet<(object, Type)> Registry { get; }
}