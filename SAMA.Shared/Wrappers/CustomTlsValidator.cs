using System.Security.Cryptography.X509Certificates;

namespace SAMA.Shared.Wrappers;

public class CustomTlsValidator
{
    public virtual bool ValidateWithCustomCa(X509Certificate? certificate, X509Chain? chain, string customCaCertificatePem)
    {
        if (certificate == null || chain == null)
        {
            return false;
        }

        try
        {
            using var certToValidate = new X509Certificate2(certificate);
            using var customCaCert = X509Certificate2.CreateFromPem(customCaCertificatePem);
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            _ = chain.ChainPolicy.CustomTrustStore.Add(customCaCert);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            return chain.Build(certToValidate);
        }
        catch
        {
            return false;
        }
    }
}
