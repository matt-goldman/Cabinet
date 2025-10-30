using demo.Services;
using demo.ViewModels;
using Microsoft.Extensions.Logging;
using Cabinet;
using Cabinet.Abstractions;
using Cabinet.Generated;
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

		// Configure offline data store using source-generated method
		builder.Services.AddSingleton<IOfflineStore>(sp =>
		{
            var OfflineKey = SecureStorage.GetAsync("CabinetMasterKey").GetAwaiter().GetResult();

            byte[] masterKey;

            if (OfflineKey == null)
            {
                // Generate and store a new master key
                var newKey = new byte[32];
                RandomNumberGenerator.Fill(newKey);
                OfflineKey = Convert.ToBase64String(newKey);
                SecureStorage.SetAsync("CabinetMasterKey", OfflineKey).GetAwaiter().GetResult();

                masterKey = newKey;
            }
            else
            {
                masterKey = Convert.FromBase64String(OfflineKey);
            }

            var rootPath = Path.Combine(FileSystem.AppDataDirectory, "Cabinet");

            // Use source-generated store creation with AOT-safe JSON serialization
            return CabinetStoreExtensions.CreateCabinetStore(
                rootPath, 
                masterKey, 
                CabinetJsonContext.Default);
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
