using Inpx.Processor.S3Storage.Implementations;
using Microsoft.Extensions.Logging;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Minnaloushe.Core.S3.S3Storage.Models;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers.Zip;

namespace Minnaloushe.Core.S3.S3Storage.CompressedStorageAdapter;

public class S3CompressedStorageAdapter(
    IS3StorageAdapter adapter,
    ITempStreamFactory tempStreamFactory,
    ILogger<S3CompressedStorageAdapter> logger)
    : IS3CompressedStorageAdapter
{
    private const string ContentEntityName = "content";

    private static readonly BlobTag CompressedTag = new("x-compressed", "true");

    public async Task GetUncompressedAsync(string key, Stream stream, CancellationToken cancellationToken = default)
    {
        logger.RequestingStream(key);

        try
        {
            // Check if the object is compressed and return directly if not
            if (!await IsCompressed(key).ConfigureAwait(false))
            {
                logger.NotCompressed(key);

                await adapter.GetStreamAsync(key, stream, cancellationToken).ConfigureAwait(false);

                return;
            }
        }
        catch (Exception ex)
        {
            logger.GetStreamFailed(ex, key);
            throw new S3StorageException($"GetUncompressedAsync: failed to get stream for key '{key}'", ex);
        }

        try
        {
            await using var tempStream = tempStreamFactory.Create();

            await adapter.GetStreamAsync(key, tempStream, cancellationToken).ConfigureAwait(false);

            logger.OpeningArchive(key);

            await using var archive = await ZipArchive
                .OpenAsyncArchive(tempStream, new ReaderOptions() { LeaveStreamOpen = true }, cancellationToken)
                .ConfigureAwait(false);

            if (await archive.EntriesAsync.AnyAsync(e => e.Key == ContentEntityName, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                logger.ReturningEntryStreamInfo(key, ContentEntityName);

                await using var zipStream = await (
                    await archive.EntriesAsync
                        .FirstAsync(f => f.Key == ContentEntityName, cancellationToken: cancellationToken)
                        .ConfigureAwait(false)
                ).OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);

                await zipStream.CopyToAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                logger.ContentEntryMissingWarning(key, ContentEntityName);
                tempStream.Seek(0, SeekOrigin.Begin);
                await tempStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            stream.Seek(0, SeekOrigin.Begin);
        }
        catch (Exception ex)
        {
            logger.UncompressingError(ex, key);
            throw new S3StorageException($"GetUncompressedAsync: failed to decompress stream for key '{key}'", ex);
        }
    }

    public async Task<BlobInfo> PutCompressedAsync(string key, Stream data, IEnumerable<BlobTag>? tags = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.PreparingCompressedUpload(key);

            // Build tags without duplicate compressed tag
            var localTagsList = new List<BlobTag>();

            if (tags != null)
            {
                localTagsList.AddRange(tags.Where(t => t.Key != CompressedTag.Key));
            }

            localTagsList.Add(CompressedTag);

            logger.UploadTags(string.Join(", ", localTagsList.Select(t => $"{t.Key}={t.Value}")));

            // Prepare an in-memory zip using SharpCompress (stream lifecycle controlled to avoid premature disposal)
            await using var zipStream = tempStreamFactory.Create();

            await using (var writer = new ZipWriter(zipStream, new ZipWriterOptions(CompressionType.Deflate) { LeaveStreamOpen = true }))
            {
                logger.CopyingContentToArchive(key);

                data.Seek(0, SeekOrigin.Begin);

                await writer.WriteAsync(ContentEntityName, data, DateTime.UtcNow, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            await zipStream.FlushAsync(cancellationToken)
                .ConfigureAwait(false);

            zipStream.Seek(0, SeekOrigin.Begin);

            logger.UploadStreamPrepared(zipStream.Length, key);

            // Upload zipped stream

            var result = await adapter.PutAsync(key, zipStream, localTagsList, contentType: "application/zip",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.CompressedUploadSuccessful(key);

            return result;
        }
        catch (Exception ex)
        {
            logger.CompressedUploadFailed(ex, key);
            throw new S3StorageException($"PutCompressedAsync: failed to upload compressed content for key '{key}'", ex);
        }
    }

    private async Task<bool> IsCompressed(string key)
    {
        logger.CheckingCompressedTag(key);

        var tags = await adapter.GetTagsAsync(key)
            .ConfigureAwait(false);

        var result = tags.Any(t => t.Equals(CompressedTag));

        logger.CompressedCheckResult(key, result);

        return result;
    }
}