namespace Minnaloushe.Core.K8s.Routines;

public static class KubernetesConstants
{
    public static string ServiceAccountTokenPath => "/var/run/secrets/kubernetes.io/serviceaccount/token";
}