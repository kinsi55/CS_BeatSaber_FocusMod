using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace FocusMod.Configuration {
	internal class PluginConfig {
		public static PluginConfig Instance { get; set; }

		public virtual float LeadTime { get; set; } = 1.5f;
		public virtual float MinimumDisplaytime { get; set; } = 0.5f;
		public virtual int MinimumNjs { get; set; } = 14;
		public virtual bool HideOnlyInHMD { get; set; } = true;
		public virtual bool UnhideInPause { get; set; } = false;

		public virtual bool IgnoreWalls { get; set; } = false;
		public virtual bool IgnoreBombs { get; set; } = false;
		public virtual bool HideAll { get; set; } = false;

		/// <summary>
		/// This is called whenever BSIPA reads the config from disk (including when file changes are detected).
		/// </summary>
		public virtual void OnReload() {
			// Do stuff after config is read from disk.
		}

		/// <summary>
		/// Call this to force BSIPA to update the config file. This is also called by BSIPA if it detects the file was modified.
		/// </summary>
		public virtual void Changed() {
			// Do stuff when the config is changed.
		}

		/// <summary>
		/// Call this to have BSIPA copy the values from <paramref name="other"/> into this config.
		/// </summary>
		public virtual void CopyFrom(PluginConfig other) {
			// This instance's members populated from other
		}
	}
}