using demo.Services;
using demo.ViewModels;
using Microsoft.Extensions.Logging;
using Plugin.Maui.OfflineData.Abstractions;
using Plugin.Maui.OfflineData.Core;
using Plugin.Maui.OfflineData.Security;

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
			// Generate a random master key for demo purposes
			// In production, this should be retrieved from SecureStorage
			var masterKey = new byte[32];
			System.Security.Cryptography.RandomNumberGenerator.Fill(masterKey);
			
			var encryptionProvider = new AesGcmEncryptionProvider(masterKey);
			var rootPath = Path.Combine(FileSystem.AppDataDirectory, "OfflineData");
			
			// Note: EasyIndex integration would go here
			// For now, we're using the store without an index provider
			// When EasyIndex is properly configured, replace null with the EasyIndex instance
			return new FileOfflineStore(rootPath, encryptionProvider, indexer: null);
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
