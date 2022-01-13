using BeatSaberMarkupLanguage.Settings;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using IPALogger = IPA.Logging.Logger;

namespace FocusMod {

	[Plugin(RuntimeOptions.SingleStartInit)]
	public class Plugin {
		internal static Plugin Instance { get; private set; }
		internal static IPALogger Log { get; private set; }

		[Init]
		/// <summary>
		/// Called when the plugin is first loaded by IPA (either when the game starts or when the plugin is enabled if it starts disabled).
		/// [Init] methods that use a Constructor or called before regular methods like InitWithConfig.
		/// Only use [Init] with one Constructor.
		/// </summary>
		public Plugin(IPALogger logger, Config conf, Zenjector zenjector) {
			Instance = this;
			Log = logger;

			PluginConfig.Instance = conf.Generated<PluginConfig>();
			zenjector.Install<FocusModInstaller>(Location.StandardPlayer);
		}

		[OnStart]
		public void OnApplicationStart() {
			BSMLSettings.instance.AddSettingsMenu("Focus Mod", "FocusMod.Views.settings.bsml", PluginConfig.Instance);
		}
	}
}
