- Add kafka security settings support
- Codsider adding group to static secrets. Group == null -> /kv/{AppNamespace}/{ServiceName}, Group != null -> /kv/{AppNamespace}/{GroupName}/{ServiceName}
    If service supports multiple auth schemes it will allow to distinguish them
- Add support for re-queue with delay policy to rabbit. Should be configurable. If set failed message should be delayed and then re-delivered. Consider how it fit with fanout exchange.
- Add error handling policy validation. NackAndRequeue/NackAndDiscard is not valid for kafka, now it just ignores it and behaves like Ack (ignore and continue)
- Add support for manual overriding topic/queue/exchange names