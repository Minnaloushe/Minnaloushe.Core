using Microsoft.Extensions.Logging;
using Minnaloushe.Core.S3.S3Storage.Adapter;
using Minnaloushe.Core.S3.S3Storage.Exceptions;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;
using Minnaloushe.Core.Toolbox.StringExtensions;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers.Tar;
using System.Text.Json;

namespace Minnaloushe.Core.S3.S3Storage.MetadataAdapter;

public class S3MetadataAdapter(
    ITempStreamFactory tempStreamFactory,
    IS3StorageAdapter adapter,
    ILogger<S3MetadataAdapter> logger
) : IS3MetadataAdapter
{
    private const string MetadataKeySuffix = "metadata";
    private static string GetKey(string originalBlobKey) => $"{originalBlobKey}/{MetadataKeySuffix}";

    public async Task GetMetadataStreamAsync(string blobKey, string metadataKey, Stream outStream,
        CancellationToken cancellationToken = default)
    {
        logger.GettingMetadataStream(blobKey, metadataKey);

        await using var tempStream = tempStreamFactory.Create();

        if (!await adapter.ExistsAsync(GetKey(blobKey), cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await adapter.GetStreamAsync(GetKey(blobKey), tempStream, cancellationToken).ConfigureAwait(false);

        await using var reader = await TarReader.OpenAsyncReader(tempStream, new ReaderOptions(), cancellationToken)
            .ConfigureAwait(false);

        while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.Entry.IsDirectory && reader.Entry.Key == metadataKey)
            {
                await using var entryStream =
                    await reader.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
                await entryStream.CopyToAsync(outStream, cancellationToken).ConfigureAwait(false);
                outStream.Position = 0;
                return;
            }
        }
    }

    public async Task<T?> GetMetadataAsync<T>(string blobKey, string metadataKey,
        CancellationToken cancellationToken = default)
    {
        logger.GettingMetadataObject(blobKey, metadataKey);

        try
        {
            await using var tempStream = tempStreamFactory.Create();

            await GetMetadataStreamAsync(blobKey, metadataKey, tempStream, cancellationToken).ConfigureAwait(false);

            if (tempStream.Length == 0)
            {
                return default;
            }

            var result = await JsonSerializer.DeserializeAsync<T>(tempStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            logger.MetadataObjectRetrieved(blobKey, metadataKey);

            return result;
        }
        catch (Exception ex) when (ex is not S3MetadataDeserializationException)
        {
            logger.GetMetadataObjectError(ex, blobKey, metadataKey);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<string>> ListMetadataKeysAsync(string blobKey,
        CancellationToken cancellationToken = default)
    {
        logger.ListingMetadataKeys(blobKey);

        try
        {
            await using var tempStream = tempStreamFactory.Create();

            await adapter.GetStreamAsync(GetKey(blobKey), tempStream, cancellationToken).ConfigureAwait(false);

            await using var archive = await TarArchive
                .OpenAsyncArchive(tempStream, new ReaderOptions(), cancellationToken).ConfigureAwait(false);

            var keys = await archive.EntriesAsync
                .Where(e => !e.IsDirectory && e.Key.IsNotNullOrWhiteSpace())
                .Select(e => e.Key!)
                .ToListAsync(cancellationToken: cancellationToken);

            logger.MetadataKeysListed(keys.Count, blobKey);
            return keys;
        }
        catch (Exception ex)
        {
            logger.ListMetadataKeysError(ex, blobKey);
            throw;
        }
    }

    public async Task PutMetadataStreamAsync(string blobKey, string metadataKey, Stream data,
        CancellationToken cancellationToken = default)
    {
        logger.PuttingMetadata(blobKey, metadataKey);

        try
        {
            await using var outStream = tempStreamFactory.Create();

            if (await adapter.ExistsAsync(GetKey(blobKey), cancellationToken).ConfigureAwait(false))
            {
                logger.ArchiveExists(blobKey);

                await using var inStream = tempStreamFactory.Create();
                await adapter.GetStreamAsync(GetKey(blobKey), inStream, cancellationToken).ConfigureAwait(false);

                // Check if metadataKey already exists
                var entryExists =
                    await CheckEntryExists(inStream, metadataKey, cancellationToken).ConfigureAwait(false);

                if (entryExists)
                {
                    // Replace existing entry
                    await ReplaceEntry(inStream, outStream, metadataKey, data, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Append new entry
                    await AppendEntry(inStream, outStream, metadataKey, data, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                logger.CreatingNewArchive(blobKey);

                // Create new archive with single entry
                await CreateNewArchive(outStream, metadataKey, data, cancellationToken).ConfigureAwait(false);
            }

            await adapter.PutAsync(GetKey(blobKey), outStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            logger.MetadataStored(blobKey, metadataKey);
        }
        catch (Exception ex)
        {
            logger.PutMetadataError(ex, blobKey, metadataKey);
            throw;
        }
    }

    public async Task PutMetadataAsync<T>(string blobKey, string metadataKey, T metadata,
        CancellationToken cancellationToken = default)
    {
        await using var tempStream = tempStreamFactory.Create();

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(metadata);
        await tempStream.WriteAsync(jsonBytes, cancellationToken).ConfigureAwait(false);
        tempStream.Seek(0, SeekOrigin.Begin);
        await PutMetadataStreamAsync(blobKey, metadataKey, tempStream, cancellationToken).ConfigureAwait(false);
    }

    public static async Task AppendEntry(
        Stream existingTar,
        Stream output, string newEntryKey,
        Stream newEntry, CancellationToken cancellationToken)
    {
        existingTar.Position = 0;

        var reader = await TarReader.OpenAsyncReader(existingTar, new ReaderOptions(), cancellationToken)
            .ConfigureAwait(false);
        await using (var writer = new TarWriter(
                         output,
                         new TarWriterOptions(CompressionType.None, true)))
        {
            // Copy existing entries
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                var entry = reader.Entry;
                if (entry.IsDirectory)
                {
                    continue;
                }

                await using var entryStream =
                    await reader.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
                if (entry.Key is null)
                {
                    continue;
                }

                await writer.WriteAsync(entry.Key, entryStream, entry.LastModifiedTime, entry.Size, cancellationToken)
                    .ConfigureAwait(false);
            }

            newEntry.Seek(0, SeekOrigin.Begin);
            await writer.WriteAsync(newEntryKey, newEntry, DateTime.UtcNow, newEntry.Length, cancellationToken)
                .ConfigureAwait(false);
        }

        output.Position = 0;
    }

    private static async Task<bool> CheckEntryExists(Stream tarStream, string entryKey,
        CancellationToken cancellationToken)
    {
        tarStream.Position = 0;

        var reader = await TarReader.OpenAsyncReader(tarStream, new ReaderOptions(), cancellationToken)
            .ConfigureAwait(false);

        while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.Entry.IsDirectory && reader.Entry.Key == entryKey)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task ReplaceEntry(
        Stream sourceTar,
        Stream output,
        string keyToReplace,
        Stream newData,
        CancellationToken cancellationToken)
    {
        sourceTar.Position = 0;

        var reader = await TarReader.OpenAsyncReader(sourceTar, new ReaderOptions(), cancellationToken)
            .ConfigureAwait(false);
        await using (var writer = new TarWriter(
                         output,
                         new TarWriterOptions(CompressionType.None, true)))
        {
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                var entry = reader.Entry;
                if (entry.IsDirectory || entry.Key is null)
                {
                    continue;
                }

                if (entry.Key == keyToReplace)
                {
                    // Replace with new data
                    newData.Seek(0, SeekOrigin.Begin);
                    await writer.WriteAsync(entry.Key, newData, DateTime.UtcNow, newData.Length, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    // Copy existing entry
                    await using var entryStream =
                        await reader.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);

                    await writer
                        .WriteAsync(entry.Key, entryStream, entry.LastModifiedTime, entry.Size, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        output.Position = 0;
    }

    private static async Task CreateNewArchive(
        Stream output,
        string entryKey,
        Stream data,
        CancellationToken cancellationToken)
    {
        using (var writer = new TarWriter(
                   output,
                   new TarWriterOptions(CompressionType.None, true)))
        {
            data.Seek(0, SeekOrigin.Begin);
            await writer.WriteAsync(entryKey, data, DateTime.UtcNow, data.Length, cancellationToken)
                .ConfigureAwait(false);
        }

        output.Position = 0;
    }

    public async Task DeleteMetadataAsync(string blobKey, string metadataKey,
        CancellationToken cancellationToken = default)
    {
        logger.DeletingMetadata(blobKey, metadataKey);

        try
        {
            if (!await adapter.ExistsAsync(GetKey(blobKey), cancellationToken).ConfigureAwait(false))
            {
                throw new S3MetadataNotFoundException(blobKey);
            }

            await using var inStream = tempStreamFactory.Create();
            await using var outStream = tempStreamFactory.Create();


            await adapter.GetStreamAsync(GetKey(blobKey), inStream, cancellationToken).ConfigureAwait(false);

            if (!await RemoveEntry(inStream, outStream, metadataKey, cancellationToken).ConfigureAwait(false))
            {
                logger.LogWarning("Remove metadata did nothing. Metadata {MetadataKey} for key {BlobKey} was not found",
                    metadataKey, blobKey);
                return;
            }

            await adapter.PutAsync(GetKey(blobKey), outStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (S3StorageException ex)
        {
            logger.DeleteMetadataError(ex, blobKey, metadataKey);
            throw;
        }
    }

    private static async Task<bool> RemoveEntry(Stream sourceTar, Stream output, string keyToRemove,
        CancellationToken cancellationToken)
    {
        sourceTar.Position = 0;

        bool isDeleted = false;
        var reader = await TarReader.OpenAsyncReader(sourceTar, new ReaderOptions(), cancellationToken)
            .ConfigureAwait(false);
        await using (var writer = new TarWriter(
                         output,
                         new TarWriterOptions(CompressionType.None, true)))
        {
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                var entry = reader.Entry;
                if (entry.IsDirectory || entry.Key == keyToRemove || entry.Key is null)
                {
                    isDeleted = true;
                    continue;
                }

                await using var entryStream =
                    await reader.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);

                await writer.WriteAsync(entry.Key, entryStream, entry.LastModifiedTime, entry.Size, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        output.Position = 0;
        return isDeleted;
    }

    public async Task DeleteAllMetadataAsync(string blobKey, CancellationToken cancellationToken = default)
    {
        logger.DeletingAllMetadata(blobKey);

        try
        {
            await adapter.DeleteAsync(GetKey(blobKey), cancellationToken).ConfigureAwait(false);

            logger.AllMetadataDeleted(blobKey);
        }
        catch (Exception ex)
        {
            logger.DeleteAllMetadataError(ex, blobKey);
            throw;
        }
    }
}