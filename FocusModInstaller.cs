using Zenject;

namespace FocusMod {
	class FocusModInstaller : MonoInstaller {
		public override void InstallBindings() {
			Container.BindInterfacesAndSelfTo<FocusMod>().AsSingle().NonLazy();

			// I dont know exactly how all of this works but its necessary and 999999 seems to be a safe bet 
			Container.BindExecutionOrder<FocusMod>(999999);
		}
	}
}
