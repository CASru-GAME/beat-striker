#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Alice {
    static class WebAddressablesCatalogVersion {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void VersionCatalogRequests() {
            var runtimePath = Addressables.RuntimePath;
            var settingsUrl = runtimePath + "/settings.json";
            var catalogUrl = runtimePath + "/catalog.bin";
            var catalogHashUrl = runtimePath + "/catalog.hash";
            var versionQuery = "?h=" + Application.buildGUID;

            // The host can cache these fixed paths across player uploads. Bundles already have hashed names.
            Addressables.WebRequestOverride += request => {
                var url = request.url;
                if (url == settingsUrl || url == catalogUrl || url == catalogHashUrl) {
                    request.url = url + versionQuery;
                }
            };
        }
    }
}
#endif
