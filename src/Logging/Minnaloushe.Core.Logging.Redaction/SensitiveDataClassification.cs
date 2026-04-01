using Microsoft.Extensions.Compliance.Classification;

namespace Minnaloushe.Core.Logging.Redaction;

public static class SensitiveDataClassification
{
    internal static DataClassification PrivateClassification => new(
        "Private",
        "Data intended for internal use only that could cause minor harm if disclosed.");
}