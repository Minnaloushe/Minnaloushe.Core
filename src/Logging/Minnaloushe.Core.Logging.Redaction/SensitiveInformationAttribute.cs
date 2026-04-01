using Microsoft.Extensions.Compliance.Classification;

namespace Minnaloushe.Core.Logging.Redaction;

public sealed class SensitiveInformationAttribute()
    : DataClassificationAttribute(SensitiveDataClassification.PrivateClassification);