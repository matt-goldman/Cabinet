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
			
			// Use simple in-memory index provider for demo
			// In production, replace with EasyIndex or another persistent index provider
			var indexProvider = new SimpleInMemoryIndexProvider();
			
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
