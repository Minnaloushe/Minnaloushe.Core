using Microsoft.Extensions.Logging;
using Minio.DataModel.Args;
using Minio.DataModel.Tags;
using Minio.Exceptions;
using Minnaloushe.Core.ClientProviders.Minio;
using Minnaloushe.Core.ClientProviders.Minio.Options;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Minnaloushe.Core.S3.S3Storage.Models;
using System.Runtime.CompilerServices;

namespace Minnaloushe.Core.S3.S3Storage.Adapter;

internal class S3StorageAdapter(
    S3StorageOptions options,
    IMinioClientProvider provider,
    ILogger<S3StorageAdapter> logger)
    : IS3StorageAdapter
{
    public virtual async Task GetStreamAsync(string key, Stream stream, CancellationToken cancellationToken = default)
    {
        logger.GettingStream(key, options.BucketName);
        try
        {
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(key)
                .WithCallbackStream(async (s, ct) =>
                {
                    await s.CopyToAsync(stream, ct);
                });

            await provider.Client.GetObjectAsync(getObjectArgs, cancellationToken).ConfigureAwait(false);
            stream.Seek(0, SeekOrigin.Begin);

            logger.StreamRetrieved(key, stream.Length);
        }
        catch (MinioException ex)
        {
            logger.GetStreamError(ex, key, options.BucketName);
            throw new S3StorageException($"Error getting stream for key '{key}' from bucket '{options.BucketName}'", ex);
        }
        catch (Exception ex)
        {
            logger.GetStreamError(ex, key, options.BucketName);
            throw new S3StorageException($"Error getting stream for key '{key}' from bucket '{options.BucketName}'", ex);
        }
    }

    public async Task<IReadOnlyCollection<BlobTag>> GetTagsAsync(string key, CancellationToken cancellationToken = default)
    {
        logger.GettingTags(key, options.BucketName);
        try
        {
            var getObjectTagsArgs = new GetObjectTagsArgs()
                .WithBucket(options.BucketName)
                .WithObject(key);

            var objectTags = await provider.Client.GetObjectTagsAsync(getObjectTagsArgs, cancellationToken)
                .ConfigureAwait(false);

            var tags = objectTags.Tags?
                           .Select(t => new BlobTag(t.Key, t.Value))
                           .ToList()
                       ?? [];

            logger.TagsRetrieved(tags.Count, key);
            return tags;
        }
        catch (MinioException ex)
        {
            logger.GetTagsError(ex, key, options.BucketName);
            throw new S3StorageException(
                $"Error getting tags for key '{key}' from bucket '{options.BucketName}'", ex);
        }
        catch (Exception ex)
        {
            logger.GetTagsError(ex, key, options.BucketName);
            throw new S3StorageException(
                $"Error getting tags for key '{key}' from bucket '{options.BucketName}'", ex);
        }
    }

    public async IAsyncEnumerable<BlobInfo> ListBlobsAsync(string prefix = "", [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in provider.Client.ListObjectsEnumAsync(
                           new ListObjectsArgs()
                               .WithBucket(options.BucketName)
                               .WithPrefix(prefix)
                               .WithRecursive(true)
                           , cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new BlobInfo()
            {
                Key = item.Key,
                Size = item.Size,
                ETag = item.ETag,
            };
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        logger.CheckingExistence(key, options.BucketName);
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(key);

            // If object exists StatObjectAsync will succeed; if it doesn't it will throw an ObjectNotFoundException
            await provider.Client.StatObjectAsync(statArgs, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (ObjectNotFoundException)
        {
            // Object does not exist
            logger.KeyNotFound(key, options.BucketName);
            return false;
        }
        catch (MinioException ex)
        {
            logger.CheckExistenceError(ex, key, options.BucketName);
            throw new S3StorageException($"Error checking existence of key '{key}' in bucket '{options.BucketName}'", ex);
        }
        catch (Exception ex)
        {
            logger.UnexpectedExistenceError(ex, key, options.BucketName);
            throw new S3StorageException($"Unexpected error checking existence of key '{key}' in bucket '{options.BucketName}'", ex);
        }
    }

    public async Task<BlobInfo> GetBlobInfoAsync(string key, CancellationToken cancellationToken = default)
    {
        logger.GettingBlobInfo(key, options.BucketName);
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(key);

            var stat = await provider.Client.StatObjectAsync(statArgs, cancellationToken)
                .ConfigureAwait(false);

            return new BlobInfo
            {
                Key = key,
                ETag = stat.ETag?.Trim('"') ?? string.Empty,
                LastModified = stat.LastModified,
                Size = (ulong)stat.Size
            };
        }
        catch (ObjectNotFoundException ex)
        {
            logger.ObjectNotFound(ex, key, options.BucketName);
            throw new S3StorageException($"Object not found: key '{key}' in bucket '{options.BucketName}'", ex);
        }
        catch (MinioException ex)
        {
            logger.GetBlobInfoError(ex, key, options.BucketName);
            throw new S3StorageException($"Error getting blob info for key '{key}' from bucket '{options.BucketName}'", ex);
        }
        catch (Exception ex)
        {
            logger.UnexpectedBlobInfoError(ex, key, options.BucketName);
            throw new S3StorageException($"Unexpected error getting blob info for key '{key}' in bucket '{options.BucketName}'", ex);
        }
    }

    public virtual async Task<BlobInfo> PutAsync(
        string key,
        Stream data,
        IEnumerable<BlobTag>? tags = null,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        logger.UploadingStream(key, options.BucketName, contentType);
        try
        {
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(key)
                .WithStreamData(data)
                .WithObjectSize(data.Length)
                .WithContentType(contentType);

            if (tags != null)
            {
                var tagDictionary = tags.ToDictionary(t => t.Key, t => t.Value);
                putObjectArgs = putObjectArgs.WithTagging(Tagging.GetObjectTags(tagDictionary));
            }

            var response = await provider.Client.PutObjectAsync(putObjectArgs, cancellationToken)
                .ConfigureAwait(false);
            logger.UploadSuccessful(key);

            return new BlobInfo()
            {
                Key = response.ObjectName,
                ETag = response.Etag.Trim('"'),
                Size = (ulong)response.Size,
                LastModified = DateTimeOffset.Now
            };
        }
        catch (MinioException ex)
        {
            logger.UploadError(ex, key, options.BucketName);
            throw new S3StorageException($"Error uploading stream for key '{key}' to bucket '{options.BucketName}'", ex);
        }
        catch (Exception ex)
        {
            logger.UploadError(ex, key, options.BucketName);
            throw new S3StorageException($"Error uploading stream for key '{key}' to bucket '{options.BucketName}'", ex);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        logger.DeletingKey(key, options.BucketName);
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(key);

            await provider.Client.RemoveObjectAsync(removeObjectArgs, cancellationToken)
                .ConfigureAwait(false);
            logger.DeleteSuccessful(key);
        }
        catch (MinioException ex)
        {
            logger.DeleteError(ex, key, options.BucketName);
            throw new S3StorageException($"Error deleting stream for key '{key}' from bucket '{options.BucketName}'", ex);
        }
        catch (Exception ex)
        {
            logger.DeleteError(ex, key, options.BucketName);
            throw new S3StorageException($"Error deleting stream for key '{key}' from bucket '{options.BucketName}'", ex);
        }
    }

    public async Task<IReadOnlyCollection<BlobInfo>> ListAsync(
        string? prefix = null,
        CancellationToken cancellationToken = default
        )
    {
        logger.ListingBlobs(options.BucketName, prefix);
        try
        {
            var listObjectsArgs = new ListObjectsArgs()
                .WithBucket(options.BucketName)
                .WithPrefix(prefix)
                .WithRecursive(true);

            var result = new List<BlobInfo>();

            await foreach (var item in provider.Client.ListObjectsEnumAsync(listObjectsArgs, cancellationToken)
                .ConfigureAwait(false))
            {
                result.Add(new BlobInfo
                {
                    Key = item.Key,
                    Size = item.Size,
                    LastModified = item.LastModifiedDateTime,
                    ETag = item.ETag?.Trim('"') ?? string.Empty
                });
            }

            logger.ListingComplete(result.Count, options.BucketName);
            return result;
        }
        catch (MinioException ex)
        {
            logger.ListError(ex, options.BucketName);
            throw new S3StorageException($"Error listing blobs in bucket '{options.BucketName}'", ex);
        }
        catch (Exception ex)
        {
            logger.ListError(ex, options.BucketName);
            throw new S3StorageException($"Error listing blobs in bucket '{options.BucketName}'", ex);
        }
    }

    public async Task<BlobInfo> RenameAsync(string existingKey, string newKey, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        logger.RenamingKey(existingKey, newKey, options.BucketName, overwrite);

        try
        {
            // Ensure source exists
            if (!await ExistsAsync(existingKey, cancellationToken)
                .ConfigureAwait(false))
            {
                logger.RenameSourceNotFound(existingKey, options.BucketName);
                throw new S3StorageException($"Source object '{existingKey}' not found in bucket '{options.BucketName}'.");
            }

            // Destination exist check
            if (!overwrite && await ExistsAsync(newKey, cancellationToken)
                .ConfigureAwait(false))
            {
                logger.RenameDestinationExists(newKey, options.BucketName);
                throw new S3StorageException($"Destination object '{newKey}' already exists in bucket '{options.BucketName}'.");
            }

            await provider.Client.CopyObjectAsync(
                new CopyObjectArgs()
                    .WithBucket(options.BucketName)
                    .WithObject(newKey)
                    .WithReplaceMetadataDirective(true)
                    .WithReplaceTagsDirective(true)
                    .WithCopyObjectSource(
                        new CopySourceObjectArgs()
                            .WithBucket(options.BucketName)
                            .WithObject(existingKey)
                    ), cancellationToken)
                .ConfigureAwait(false);
            // Delete original only after successful upload
            await DeleteAsync(existingKey, cancellationToken)
                .ConfigureAwait(false);

            logger.RenameSuccessful(existingKey, newKey, options.BucketName);

            return await GetBlobInfoAsync(newKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (S3StorageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.RenameFailed(ex, existingKey, newKey, options.BucketName);
            throw new S3StorageException($"Failed to rename '{existingKey}' to '{newKey}' in bucket '{options.BucketName}'", ex);
        }
    }
}
