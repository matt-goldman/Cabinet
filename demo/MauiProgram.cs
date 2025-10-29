using demo.Services;
using demo.ViewModels;
using Microsoft.Extensions.Logging;
using Cabinet.Abstractions;
using Cabinet.Core;
using Cabinet.Index;
using Cabinet.Security;
using System.Security.Cryptography;

namespace demo;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Configure offline data store
		builder.Services.AddSingleton<IOfflineStore>(sp =>
		{
            var OfflineKey = SecureStorage.GetAsync("OfflineDataMasterKey").GetAwaiter().GetResult();

            byte[] masterKey;

            if (OfflineKey == null)
            {
                // Generate and store a new master key
                var newKey = new byte[32];
                RandomNumberGenerator.Fill(newKey);
                OfflineKey = Convert.ToBase64String(newKey);
                SecureStorage.SetAsync("OfflineDataMasterKey", OfflineKey).GetAwaiter().GetResult();

                masterKey = newKey;
            }
            else
            {
                masterKey = Convert.FromBase64String(OfflineKey);
            }

            var encryptionProvider = new AesGcmEncryptionProvider(masterKey);
            var rootPath = Path.Combine(FileSystem.AppDataDirectory, "OfflineData");

            // Use persistent index provider that stores encrypted index to disk
            var indexProvider = new PersistentIndexProvider(rootPath, encryptionProvider);

            return new FileOfflineStore(rootPath, encryptionProvider, indexProvider);
        });

		builder.Services.AddSingleton<OfflineDataService>();
		builder.Services.AddSingleton<MainViewModel>();
		builder.Services.AddSingleton<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
