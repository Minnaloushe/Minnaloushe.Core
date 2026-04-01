using System.Text;
using NLog;
using NLog.LayoutRenderers;

namespace Minnaloushe.Core.Logging.NLog;

[LayoutRenderer("category-hash")]
internal class CategoryHashLayoutRenderer : LayoutRenderer
{
    protected override void Append(StringBuilder builder, LogEventInfo logEvent)
    {
        var loggerName = logEvent.LoggerName;
        if (string.IsNullOrEmpty(loggerName))
            return;

        builder.Append(loggerName.GetHashCode().ToString("x8"));
    }
}