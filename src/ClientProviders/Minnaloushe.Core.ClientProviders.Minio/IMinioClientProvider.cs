using Minio;
using Minnaloushe.Core.ClientProviders.Abstractions.StaticClientProvider;
using Minnaloushe.Core.Toolbox.AsyncInitializer;

namespace Minnaloushe.Core.ClientProviders.Minio;

public interface IMinioClientProvider : IStaticClientProvider<IMinioClient>, IAsyncInitializer;
