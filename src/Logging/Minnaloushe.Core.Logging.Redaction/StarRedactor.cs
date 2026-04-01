using Microsoft.Extensions.Compliance.Redaction;

namespace Minnaloushe.Core.Logging.Redaction;

public sealed class StarRedactor : Redactor
{
    private const int StartLen = 4;
    private const int EndLen = 4;

    // We omit any trailing ":FieldName" (including the colon) from the redacted output.
    public override int GetRedactedLength(ReadOnlySpan<char> input)
    {
        var sepIndex = input.LastIndexOf(':');
        if (sepIndex >= 0 && sepIndex < input.Length - 1)
        {
            // return length of prefix only (exclude ':' and field name)
            return sepIndex;
        }

        return input.Length;
    }

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (source.Length == 0)
        {
            return 0;
        }

        // Determine prefix length (exclude trailing ":FieldName" if present)
        var sepIndex = source.LastIndexOf(':');
        var prefixLen = (sepIndex >= 0 && sepIndex < source.Length - 1) ? sepIndex : source.Length;
        var prefix = source[..prefixLen];

        if (prefixLen == 0)
        {
            // nothing to mask in prefix, copy nothing (we omit suffix)
            return 0;
        }

        var L = prefixLen;

        // Ensure at least 50% is masked -> kept <= floor(L/2)
        var allowedKeep = L / 2; // floor

        var headKeep = Math.Min(StartLen, L);
        var tailKeep = Math.Min(EndLen, Math.Max(0, L - headKeep));

        if (headKeep + tailKeep > allowedKeep)
        {
            var excess = headKeep + tailKeep - allowedKeep;
            // reduce tail first
            var reduceTail = Math.Min(excess, tailKeep);
            tailKeep -= reduceTail;
            excess -= reduceTail;
            if (excess > 0)
            {
                var reduceHead = Math.Min(excess, headKeep);
                headKeep -= reduceHead;
            }
        }

        var middleLen = L - headKeep - tailKeep;

        var pos = 0;

        // copy head
        if (headKeep > 0)
        {
            prefix[..headKeep].CopyTo(destination.Slice(pos, headKeep));
            pos += headKeep;
        }

        // fill masked middle using Span.Fill (no per-char allocations)
        if (middleLen > 0)
        {
            destination.Slice(pos, middleLen).Fill('*');
            pos += middleLen;
        }

        // copy tail
        if (tailKeep > 0)
        {
            prefix.Slice(L - tailKeep, tailKeep).CopyTo(destination.Slice(pos, tailKeep));
            pos += tailKeep;
        }

        // do not append suffix (omit ":FieldName" entirely)
        return pos;
    }
}